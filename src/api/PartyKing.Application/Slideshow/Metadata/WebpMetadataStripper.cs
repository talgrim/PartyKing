using System.Buffers.Binary;
using System.Text;

namespace PartyKing.Application.Slideshow.Metadata;

/// <summary>
/// WebP (RIFF): keeps the chunks that render the image (bitstream, alpha, animation, ICC profile) and drops EXIF, XMP
/// and unknown chunks and all bytes after the RIFF. The EXIF and XMP flags of the extended header are cleared and the
/// RIFF size is written again.
/// </summary>
internal sealed class WebpMetadataStripper : IImageFormatStripper
{
    private const int RiffHeaderLength = 12;
    private const int ChunkHeaderLength = 8;
    private const int ExtendedHeaderLength = 10;
    private const byte MetadataFlags = 0x08 | 0x04;

    private static readonly HashSet<string> KeptChunks = ["VP8 ", "VP8L", "VP8X", "ALPH", "ANIM", "ANMF", "ICCP"];

    public string Extension => ".webp";

    public MemoryStream? Strip(ReadOnlySpan<byte> data)
    {
        if (data.Length < RiffHeaderLength || !data[..4].SequenceEqual("RIFF"u8) || !data[8..12].SequenceEqual("WEBP"u8))
        {
            return null;
        }

        var riffSize = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        if (riffSize < 4 || riffSize > data.Length - 8)
        {
            return null;
        }

        var end = 8 + (int)riffSize;
        // The header is written last, when the size of the chunks is known; the chunks follow it in the same buffer
        var output = new MemoryStream(end);
        output.Write(stackalloc byte[RiffHeaderLength]);
        var position = RiffHeaderLength;

        while (position < end)
        {
            if (position + ChunkHeaderLength > end)
            {
                return null;
            }

            var type = Encoding.ASCII.GetString(data.Slice(position, 4));
            var size = BinaryPrimitives.ReadUInt32LittleEndian(data[(position + 4)..]);
            if (size > end - position - ChunkHeaderLength)
            {
                return null;
            }

            var payloadStart = position + ChunkHeaderLength;
            var payloadEnd = payloadStart + (int)size;
            if (KeptChunks.Contains(type))
            {
                if (type == "VP8X" && size < ExtendedHeaderLength)
                {
                    return null;
                }

                WriteChunk(output, type, data[payloadStart..payloadEnd]);
            }

            // The pad byte of an odd chunk may be missing at the very end of the file
            position = Math.Min(payloadEnd + (int)(size & 1), end);
        }

        if (output.Length == RiffHeaderLength)
        {
            return null;
        }

        var header = output.GetBuffer().AsSpan(0, RiffHeaderLength);
        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], (uint)output.Length - 8);
        "WEBP"u8.CopyTo(header[8..]);

        return StrippedImage.Of(output);
    }

    private static void WriteChunk(MemoryStream output, string type, ReadOnlySpan<byte> payload)
    {
        output.Write(Encoding.ASCII.GetBytes(type));
        WriteUInt32(output, (uint)payload.Length);

        if (type == "VP8X")
        {
            var header = payload.ToArray();
            header[0] &= unchecked((byte)~MetadataFlags);
            output.Write(header);
        }
        else
        {
            output.Write(payload);
        }

        if ((payload.Length & 1) == 1)
        {
            output.WriteByte(0x00);
        }
    }

    private static void WriteUInt32(MemoryStream output, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        output.Write(bytes);
    }
}
