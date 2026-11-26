namespace PartyKing.Application.Slideshow.Metadata;

/// <summary>
/// GIF: keeps the screen, the colour tables, the frames and the extensions that change how they are shown (graphic
/// control, loop count, colour profile) and drops comments, plain text, every other application extension (XMP) and all
/// bytes after the trailer. A file that ends cleanly after a frame, without the trailer, gets it: the whole file has been
/// read, so nothing is left over that could carry data.
/// </summary>
internal sealed class GifMetadataStripper : IImageFormatStripper
{
    private const int HeaderLength = 13;
    private const byte Trailer = 0x3B;
    private const byte ExtensionIntroducer = 0x21;
    private const byte ImageSeparator = 0x2C;
    private const byte GraphicControlLabel = 0xF9;
    private const byte ApplicationLabel = 0xFF;
    private const int ApplicationIdentifierLength = 11;

    private static readonly byte[][] KeptApplications =
    [
        "NETSCAPE2.0"u8.ToArray(),
        "ANIMEXTS1.0"u8.ToArray(),
        "ICCRGBG1012"u8.ToArray()
    ];

    public string Extension => ".gif";

    public MemoryStream? Strip(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderLength || !(data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8)))
        {
            return null;
        }

        var position = HeaderLength + ColorTableLength(data[10]);
        if (position > data.Length)
        {
            return null;
        }

        var output = new MemoryStream(data.Length);
        output.Write(data[..position]);

        var frameSeen = false;
        while (position < data.Length)
        {
            var introducer = data[position];
            if (introducer == Trailer)
            {
                output.WriteByte(Trailer);
                return StrippedImage.Of(output);
            }

            if (introducer == ExtensionIntroducer)
            {
                if (position + 2 > data.Length)
                {
                    return null;
                }

                var blocksStart = position + 2;
                var end = SkipSubBlocks(data, blocksStart);
                if (end < 0)
                {
                    return null;
                }

                if (IsKept(data[position + 1], data[blocksStart..end]))
                {
                    output.Write(data[position..end]);
                }

                position = end;
            }
            else if (introducer == ImageSeparator)
            {
                if (position + 10 > data.Length)
                {
                    return null;
                }

                // Descriptor (10 bytes), the local colour table, the LZW code size (1 byte), then the data blocks
                var dataStart = position + 10 + ColorTableLength(data[position + 9]) + 1;
                if (dataStart > data.Length)
                {
                    return null;
                }

                var end = SkipSubBlocks(data, dataStart);
                if (end < 0)
                {
                    return null;
                }

                output.Write(data[position..end]);
                frameSeen = true;
                position = end;
            }
            else
            {
                return null;
            }
        }

        // Every block was read to its end and the file stops there: only the trailer is missing
        if (!frameSeen)
        {
            return null;
        }

        output.WriteByte(Trailer);

        return StrippedImage.Of(output);
    }

    /// <summary>The size of the colour table a packed byte announces; 0 when there is none.</summary>
    private static int ColorTableLength(byte packed)
    {
        return (packed & 0x80) == 0 ? 0 : 3 << ((packed & 0x07) + 1);
    }

    /// <summary>The index after the terminator of the data blocks that start at <paramref name="position"/>; -1 when they are cut.</summary>
    private static int SkipSubBlocks(ReadOnlySpan<byte> data, int position)
    {
        while (position < data.Length)
        {
            var size = data[position];
            position += 1 + size;
            if (size == 0)
            {
                return position <= data.Length ? position : -1;
            }
        }

        return -1;
    }

    private static bool IsKept(byte label, ReadOnlySpan<byte> blocks)
    {
        if (label == GraphicControlLabel)
        {
            return true;
        }

        if (label != ApplicationLabel || blocks.Length <= ApplicationIdentifierLength || blocks[0] != ApplicationIdentifierLength)
        {
            return false;
        }

        var identifier = blocks.Slice(1, ApplicationIdentifierLength);
        foreach (var application in KeptApplications)
        {
            if (identifier.SequenceEqual(application))
            {
                return true;
            }
        }

        return false;
    }
}
