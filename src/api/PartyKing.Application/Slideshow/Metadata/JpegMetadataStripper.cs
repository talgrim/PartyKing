using System.Buffers.Binary;

namespace PartyKing.Application.Slideshow.Metadata;

/// <summary>
/// JPEG: keeps only what decodes the image (frame, tables, scans, the JFIF header without its thumbnail, the ICC
/// profile, the Adobe colour transform) and drops every other segment: Exif with GPS, XMP, IPTC, MPF, comments, and the
/// reserved and extension markers, as well as all bytes after the end of the image (motion photos append a video there).
/// The Exif orientation is written back as a minimal Exif segment holding nothing else, right after the JFIF header.
/// An image that does not end with its end marker is refused: the bytes after its last scan would be copied as image data.
/// </summary>
internal sealed class JpegMetadataStripper : IImageFormatStripper
{
    private const byte Prefix = 0xFF;
    private const byte StartOfScan = 0xDA;
    private const byte EndOfImage = 0xD9;
    private const byte App0 = 0xE0;
    private const byte App1 = 0xE1;
    private const byte App2 = 0xE2;
    private const byte App14 = 0xEE;
    private const int OrientationTag = 0x0112;
    private const int JfifHeaderLength = 12;
    private const int OrientationSegmentLength = 36;

    private static ReadOnlySpan<byte> ExifSignature => "Exif\0\0"u8;
    private static ReadOnlySpan<byte> JfifSignature => "JFIF\0"u8;
    private static ReadOnlySpan<byte> IccSignature => "ICC_PROFILE\0"u8;
    private static ReadOnlySpan<byte> AdobeSignature => "Adobe"u8;

    public string Extension => ".jpg";

    public MemoryStream? Strip(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4 || data[0] != Prefix || data[1] != 0xD8)
        {
            return null;
        }

        // Nothing kept is longer than its source, apart from the orientation segment that is added at the end
        var output = new MemoryStream(data.Length + OrientationSegmentLength);
        output.WriteByte(Prefix);
        output.WriteByte(0xD8);

        var orientation = 0;
        var orientationAt = 2;
        var exifSeen = false;
        var scanSeen = false;
        var position = 2;

        while (true)
        {
            if (position >= data.Length || data[position] != Prefix)
            {
                return null;
            }

            while (position < data.Length && data[position] == Prefix)
            {
                position++;
            }

            if (position >= data.Length)
            {
                return null;
            }

            var marker = data[position++];
            if (marker == EndOfImage)
            {
                break;
            }

            // A stuffed zero, TEM, RSTn and a second SOI have no place between the segments of the header
            if (marker is 0x00 or 0x01 or (>= 0xD0 and <= 0xD8) || position + 2 > data.Length)
            {
                return null;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(data[position..]);
            if (length < 2 || position + length > data.Length)
            {
                return null;
            }

            var segment = data.Slice(position, length);
            var payload = segment[2..];
            var segmentEnd = position + length;

            if (marker == StartOfScan)
            {
                var scanEnd = FindEndOfScan(data, segmentEnd);
                if (scanEnd < 0)
                {
                    return null;
                }

                scanSeen = true;
                WriteSegment(output, marker, data[position..scanEnd]);
                position = scanEnd;
                continue;
            }

            if (marker == App1)
            {
                if (!exifSeen && payload.StartsWith(ExifSignature))
                {
                    orientation = ReadOrientation(payload[ExifSignature.Length..]);
                    exifSeen = true;
                }
            }
            else if (marker == App0)
            {
                if (payload.StartsWith(JfifSignature) && payload.Length >= JfifHeaderLength + 2)
                {
                    var first = output.Length == 2;
                    WriteJfif(output, payload[..JfifHeaderLength]);
                    if (first)
                    {
                        orientationAt = (int)output.Length;
                    }
                }
            }
            else if (marker == App2)
            {
                if (payload.StartsWith(IccSignature))
                {
                    WriteSegment(output, marker, segment);
                }
            }
            else if (marker == App14)
            {
                if (payload.StartsWith(AdobeSignature))
                {
                    WriteSegment(output, marker, segment);
                }
            }
            else if (DecodesTheImage(marker))
            {
                WriteSegment(output, marker, segment);
            }

            // Anything else (the other APPn, comments, reserved and extension markers) is dropped

            position = segmentEnd;
        }

        if (!scanSeen)
        {
            return null;
        }

        output.WriteByte(Prefix);
        output.WriteByte(EndOfImage);

        if (orientation != 0)
        {
            InsertOrientation(output, orientationAt, BuildOrientationSegment(orientation));
        }

        return StrippedImage.Of(output);
    }

    /// <summary>The frame headers and tables a decoder needs; the reserved JPG marker (0xC8) is not one of them.</summary>
    private static bool DecodesTheImage(byte marker)
    {
        return marker is >= 0xC0 and <= 0xCF and not 0xC8 or >= 0xDB and <= 0xDF;
    }

    /// <summary>Opens a gap at <paramref name="offset"/> inside the buffer that holds the image and fills it.</summary>
    private static void InsertOrientation(MemoryStream output, int offset, byte[] segment)
    {
        var length = (int)output.Length;
        output.SetLength(length + segment.Length);

        var buffer = output.GetBuffer();
        Array.Copy(buffer, offset, buffer, offset + segment.Length, length - offset);
        segment.CopyTo(buffer, offset);
    }

    /// <summary>
    /// Where the next marker after the entropy-coded data of a scan starts, or -1 when the data ends first. Stuffed zeros,
    /// restart markers and fill bytes belong to the data.
    /// </summary>
    private static int FindEndOfScan(ReadOnlySpan<byte> data, int start)
    {
        var index = start;
        while (true)
        {
            var offset = data[index..].IndexOf(Prefix);
            if (offset < 0)
            {
                return -1;
            }

            index += offset;
            if (index + 1 >= data.Length)
            {
                return -1;
            }

            var next = data[index + 1];
            if (next == 0x00 || next is >= 0xD0 and <= 0xD7)
            {
                index += 2;
            }
            else if (next == Prefix)
            {
                index += 1;
            }
            else
            {
                return index;
            }
        }
    }

    /// <summary>The marker, then the bytes from the length field on (<paramref name="lengthAndPayload"/>).</summary>
    private static void WriteSegment(MemoryStream output, byte marker, ReadOnlySpan<byte> lengthAndPayload)
    {
        output.WriteByte(Prefix);
        output.WriteByte(marker);
        output.Write(lengthAndPayload);
    }

    /// <summary>The JFIF header with a zero-sized thumbnail: the thumbnail is a second, unedited copy of the photo.</summary>
    private static void WriteJfif(MemoryStream output, ReadOnlySpan<byte> header)
    {
        output.WriteByte(Prefix);
        output.WriteByte(App0);
        output.WriteByte(0x00);
        output.WriteByte((byte)(2 + JfifHeaderLength + 2));
        output.Write(header);
        output.WriteByte(0x00);
        output.WriteByte(0x00);
    }

    /// <summary>The orientation (2 to 8) of the TIFF structure inside an Exif segment; 0 when it is 1, missing or damaged.</summary>
    private static int ReadOrientation(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 8)
        {
            return 0;
        }

        var littleEndian = tiff[0] == 0x49 && tiff[1] == 0x49;
        var bigEndian = tiff[0] == 0x4D && tiff[1] == 0x4D;
        if ((!littleEndian && !bigEndian) || ReadUInt16(tiff, 2, littleEndian) != 42)
        {
            return 0;
        }

        var directory = ReadUInt32(tiff, 4, littleEndian);
        if (directory > (uint)(tiff.Length - 2))
        {
            return 0;
        }

        var count = ReadUInt16(tiff, (int)directory, littleEndian);
        for (var i = 0; i < count; i++)
        {
            var entry = (int)directory + 2 + i * 12;
            if (entry + 12 > tiff.Length)
            {
                return 0;
            }

            if (ReadUInt16(tiff, entry, littleEndian) != OrientationTag)
            {
                continue;
            }

            var isOneShort = ReadUInt16(tiff, entry + 2, littleEndian) == 3 && ReadUInt32(tiff, entry + 4, littleEndian) == 1;
            var value = ReadUInt16(tiff, entry + 8, littleEndian);

            return isOneShort && value is >= 2 and <= 8 ? value : 0;
        }

        return 0;
    }

    private static int ReadUInt16(ReadOnlySpan<byte> data, int offset, bool littleEndian)
    {
        return littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(data[offset..])
            : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset, bool littleEndian)
    {
        return littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(data[offset..])
            : BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
    }

    /// <summary>An Exif segment (big-endian) that holds the orientation and nothing else.</summary>
    private static byte[] BuildOrientationSegment(int orientation)
    {
        return
        [
            Prefix, App1, 0x00, 0x22,
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00,
            (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,
            0x00, 0x01,
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, (byte)orientation, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        ];
    }
}
