using System.Buffers.Binary;
using System.Text;

namespace PartyKing.Application.Slideshow.Metadata;

/// <summary>
/// PNG: keeps the chunks that render the image (header, palette, data, transparency, colour and animation chunks) and
/// drops everything else (eXIf, text, time, unknown) and all bytes after IEND.
/// </summary>
internal sealed class PngMetadataStripper : IImageFormatStripper
{
    private const int SignatureLength = 8;
    private const int ChunkOverhead = 12;
    private const int HeaderLength = 13;

    private static readonly HashSet<string> KeptChunks =
    [
        "IHDR", "PLTE", "tRNS", "IDAT", "IEND",
        "gAMA", "cHRM", "sRGB", "iCCP", "sBIT", "cICP", "mDCV", "cLLI", "pHYs", "bKGD", "hIST",
        "acTL", "fcTL", "fdAT"
    ];

    private static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public string Extension => ".png";

    public MemoryStream? Strip(ReadOnlySpan<byte> data)
    {
        if (data.Length < SignatureLength + ChunkOverhead || !data.StartsWith(Signature))
        {
            return null;
        }

        var output = new MemoryStream(data.Length);
        output.Write(Signature);

        var position = SignatureLength;
        var first = true;
        while (position + ChunkOverhead <= data.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(data[position..]);
            if (length > data.Length - position - ChunkOverhead)
            {
                return null;
            }

            var type = Encoding.ASCII.GetString(data.Slice(position + 4, 4));
            if (first && (type != "IHDR" || length != HeaderLength))
            {
                return null;
            }

            first = false;

            var chunkEnd = position + ChunkOverhead + (int)length;
            if (KeptChunks.Contains(type))
            {
                output.Write(data[position..chunkEnd]);
            }

            position = chunkEnd;
            if (type == "IEND")
            {
                return StrippedImage.Of(output);
            }
        }

        return null;
    }
}
