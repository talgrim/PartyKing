using System.Buffers.Binary;
using System.Text;

namespace PartyKing.Application.Tests;

/// <summary>
/// Small, structurally valid images that carry personal data, built byte by byte (the repository holds no photos). The
/// pixel data is not real: the strippers only walk the containers.
/// </summary>
internal static class ImageFixtures
{
    public const string Sentinel = "SENTINEL";

    /// <summary>The GPS latitude numerator in the Exif of <see cref="Jpeg"/>; it must not survive.</summary>
    public const uint GpsNumerator = 0xCAFEBABE;

    public static readonly byte[] JpegScan = [0x12, 0xFF, 0x00, 0x34, 0xFF, 0xD0, 0x56];

    /// <summary>
    /// SOI, JFIF with a thumbnail, Exif (description, orientation, GPS), XMP, IPTC, a comment, ICC, MPF, Adobe, tables,
    /// a frame and a scan, EOI and a video appended after it.
    /// </summary>
    public static byte[] Jpeg(int orientation = 6, bool littleEndian = true, bool withTrailingData = true)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };
        bytes.AddRange(Segment(0xE0, Concat("JFIF\0"u8, [1, 1, 0, 0, 1, 0, 1, 1, 1, 9, 9, 9])));
        bytes.AddRange(Segment(0xE1, Concat("Exif\0\0"u8, Tiff(orientation, littleEndian))));
        bytes.AddRange(Segment(0xE1, Ascii($"http://ns.adobe.com/xap/1.0/\0<x:xmpmeta>{Sentinel}-XMP</x:xmpmeta>")));
        bytes.AddRange(Segment(0xED, Ascii($"Photoshop 3.0\0{Sentinel}-IPTC")));
        bytes.AddRange(Segment(0xFE, Ascii($"{Sentinel}-COMMENT")));
        bytes.AddRange(Segment(0xE2, Concat("ICC_PROFILE\0"u8, [1, 1, 7, 7, 7])));
        bytes.AddRange(Segment(0xE2, Concat("MPF\0"u8, Ascii($"{Sentinel}-MPF"))));
        bytes.AddRange(Segment(0xEE, Concat("Adobe"u8, [0, 100, 0, 0, 0, 0, 1])));
        bytes.AddRange(Segment(0xDB, [0, 16, 11, 12, 14]));
        bytes.AddRange(Segment(0xC0, [8, 0, 1, 0, 1, 1, 1, 0x11, 0]));
        bytes.AddRange(Segment(0xC4, [0, 1, 0, 0, 0]));
        bytes.AddRange(Segment(0xDA, [1, 1, 0, 0, 0x3F, 0]));
        bytes.AddRange(JpegScan);
        bytes.AddRange([0xFF, 0xD9]);
        if (withTrailingData)
        {
            bytes.AddRange(Ascii($"{Sentinel}-VIDEO"));
        }

        return bytes.ToArray();
    }

    /// <summary>A big-endian Exif segment's TIFF part with only a description, an orientation and a GPS IFD.</summary>
    private static byte[] Tiff(int orientation, bool littleEndian)
    {
        var tiff = new List<byte>();
        void U16(int value) => tiff.AddRange(Number((uint)value, 2, littleEndian));
        void U32(uint value) => tiff.AddRange(Number(value, 4, littleEndian));

        var description = Ascii($"{Sentinel}-DESCRIPTION\0");

        tiff.AddRange(littleEndian ? "II"u8.ToArray() : "MM"u8.ToArray());
        U16(42);
        U32(8);

        // IFD0 at 8: three entries, the next IFD, then the description, then the GPS IFD
        U16(3);
        U16(0x010E); U16(2); U32((uint)description.Length); U32(50);
        U16(0x0112); U16(3); U32(1); U16(orientation); U16(0);
        U16(0x8825); U16(4); U32(1); U32((uint)(50 + description.Length + 1 & ~1));
        U32(0);

        tiff.AddRange(description);
        if (tiff.Count % 2 == 1)
        {
            tiff.Add(0);
        }

        // GPS IFD: the latitude reference inline and the latitude as three rationals after the IFD
        var rationals = (uint)tiff.Count + 2 + 24 + 4;
        U16(2);
        U16(1); U16(2); U32(2); tiff.AddRange("N\0\0\0"u8.ToArray());
        U16(2); U16(5); U32(3); U32(rationals);
        U32(0);

        for (var i = 0; i < 3; i++)
        {
            U32(GpsNumerator);
            U32(1);
        }

        return tiff.ToArray();
    }

    public static byte[] Png(bool withTrailingData = true)
    {
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        bytes.AddRange(PngChunk("IHDR", [0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0]));
        bytes.AddRange(PngChunk("gAMA", [0, 1, 0x86, 0xA0]));
        bytes.AddRange(PngChunk("sRGB", [0]));
        bytes.AddRange(PngChunk("pHYs", [0, 0, 0x0E, 0xC4, 0, 0, 0x0E, 0xC4, 1]));
        bytes.AddRange(PngChunk("tEXt", Ascii($"Comment\0{Sentinel}-TEXT")));
        bytes.AddRange(PngChunk("eXIf", Ascii($"MM\0*{Sentinel}-EXIF")));
        bytes.AddRange(PngChunk("tIME", [7, 0xE9, 1, 2, 3, 4, 5]));
        bytes.AddRange(PngChunk("iTXt", Ascii($"XML:com.adobe.xmp\0\0\0\0\0{Sentinel}-XMP")));
        bytes.AddRange(PngChunk("IDAT", [0x78, 0x9C, 1, 2, 3, 4]));
        bytes.AddRange(PngChunk("IEND", []));
        if (withTrailingData)
        {
            bytes.AddRange(Ascii($"{Sentinel}-TRAILING"));
        }

        return bytes.ToArray();
    }

    /// <summary>An extended WebP with alpha, an odd-sized bitstream chunk, EXIF and XMP.</summary>
    public static byte[] Webp()
    {
        var chunks = new List<byte>();
        chunks.AddRange(WebpChunk("VP8X", [0x10 | 0x08 | 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0]));
        chunks.AddRange(WebpChunk("VP8L", [0x2F, 1, 2, 3, 4]));
        chunks.AddRange(WebpChunk("EXIF", Ascii($"MM\0*{Sentinel}-EXIF")));
        chunks.AddRange(WebpChunk("XMP ", Ascii($"<x:xmpmeta>{Sentinel}-XMP</x:xmpmeta>")));

        var bytes = new List<byte>();
        bytes.AddRange("RIFF"u8.ToArray());
        bytes.AddRange(Number((uint)chunks.Count + 4, 4, littleEndian: true));
        bytes.AddRange("WEBP"u8.ToArray());
        bytes.AddRange(chunks);

        return bytes.ToArray();
    }

    /// <summary>An animated GIF: a loop count, a comment, XMP, one frame with a graphic control, and a trailer.</summary>
    public static byte[] Gif(bool withTrailingData = true)
    {
        var bytes = new List<byte>();
        bytes.AddRange("GIF89a"u8.ToArray());
        bytes.AddRange([1, 0, 1, 0, 0x80, 0, 0]);
        bytes.AddRange([0, 0, 0, 255, 255, 255]);

        // NETSCAPE2.0 loop count
        bytes.AddRange([0x21, 0xFF, 11]);
        bytes.AddRange("NETSCAPE2.0"u8.ToArray());
        bytes.AddRange([3, 1, 0, 0, 0]);

        bytes.AddRange([0x21, 0xFE]);
        bytes.AddRange(GifBlocks(Ascii($"{Sentinel}-COMMENT")));

        bytes.AddRange([0x21, 0xFF, 11]);
        bytes.AddRange("XMP DataXMP"u8.ToArray());
        bytes.AddRange(GifBlocks(Ascii($"<x:xmpmeta>{Sentinel}-XMP</x:xmpmeta>")));

        bytes.AddRange([0x21, 0xF9, 4, 0, 10, 0, 0, 0]);

        bytes.AddRange([0x2C, 0, 0, 0, 0, 1, 0, 1, 0, 0]);
        bytes.Add(2);
        bytes.AddRange(GifBlocks([0x44, 0x01]));

        bytes.Add(0x3B);
        if (withTrailingData)
        {
            bytes.AddRange(Ascii($"{Sentinel}-TRAILING"));
        }

        return bytes.ToArray();
    }

    /// <summary>The data blocks of a GIF: the length-prefixed chunks and the zero terminator.</summary>
    private static byte[] GifBlocks(byte[] data)
    {
        return [(byte)data.Length, ..data, 0];
    }

    public static byte[] Segment(byte marker, byte[] payload)
    {
        var length = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)(payload.Length + 2));

        return [0xFF, marker, ..length, ..payload];
    }

    private static byte[] PngChunk(string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);

        return [..length, ..Ascii(type), ..data, 0, 0, 0, 0];
    }

    private static byte[] WebpChunk(string type, byte[] payload)
    {
        var padding = payload.Length % 2 == 1 ? new byte[] { 0 } : [];

        return [..Ascii(type), ..Number((uint)payload.Length, 4, littleEndian: true), ..payload, ..padding];
    }

    public static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    public static bool Contains(byte[] data, string text) => data.AsSpan().IndexOf(Ascii(text)) >= 0;

    public static bool Contains(byte[] data, byte[] sequence) => data.AsSpan().IndexOf(sequence) >= 0;

    private static byte[] Concat(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        return [..first, ..second];
    }

    private static byte[] Number(uint value, int size, bool littleEndian)
    {
        var bytes = new byte[size];
        for (var i = 0; i < size; i++)
        {
            var shift = 8 * (littleEndian ? i : size - 1 - i);
            bytes[i] = (byte)(value >> shift);
        }

        return bytes;
    }
}
