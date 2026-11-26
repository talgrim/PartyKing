using PartyKing.Application.Slideshow.Metadata;
using PartyKing.Domain.Entities.Slideshow;

namespace PartyKing.Application.Tests;

public class ImageMetadataStripperTests
{
    private static readonly ImageFormat Jpeg = new(".jpg", "image/jpeg");
    private static readonly ImageFormat Png = new(".png", "image/png");
    private static readonly ImageFormat Gif = new(".gif", "image/gif");
    private static readonly ImageFormat Webp = new(".webp", "image/webp");

    private static readonly IImageFormatStripper[] Strippers =
    [
        new JpegMetadataStripper(),
        new PngMetadataStripper(),
        new WebpMetadataStripper(),
        new GifMetadataStripper()
    ];

    private readonly ImageMetadataStripper _stripper = new(Strippers);

    private async Task<byte[]> StripAsync(byte[] data, ImageFormat format)
    {
        var result = await _stripper.StripAsync(new MemoryStream(data), format, "photo" + format.Extension, CancellationToken.None);

        Assert.False(result.IsError);
        await using var stream = result.Value;
        var output = new MemoryStream();
        await stream.CopyToAsync(output);

        return output.ToArray();
    }

    private async Task AssertRejectedAsync(byte[] data, ImageFormat format)
    {
        var result = await _stripper.StripAsync(new MemoryStream(data), format, "photo" + format.Extension, CancellationToken.None);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task ADamagedFile_IsRejectedWithAReasonThatNamesIt()
    {
        var result = await _stripper.StripAsync(new MemoryStream([0xFF, 0xD8, 0xFF, 0x00]), Jpeg, "dir/holiday.jpg", CancellationToken.None);

        Assert.Equal("'holiday.jpg' is damaged and could not be processed.", Assert.Single(result.Errors).Description);
    }

    // ----- JPEG -----

    /// <summary>The markers of the segments before the scan, and the segment's bytes.</summary>
    private static List<(byte Marker, byte[] Payload)> JpegSegments(byte[] jpeg)
    {
        var segments = new List<(byte, byte[])>();
        var position = 2;
        while (position < jpeg.Length && jpeg[position] == 0xFF && jpeg[position + 1] != 0xD9)
        {
            var marker = jpeg[position + 1];
            var length = (jpeg[position + 2] << 8) | jpeg[position + 3];
            segments.Add((marker, jpeg[(position + 4)..(position + 2 + length)]));
            position += 2 + length;
            if (marker == 0xDA)
            {
                break;
            }
        }

        return segments;
    }

    private static int ExifOrientation(byte[] jpeg)
    {
        var exif = JpegSegments(jpeg).Single(x => x.Marker == 0xE1).Payload;
        var tiff = exif[6..];
        var entries = (tiff[8] << 8) | tiff[9];
        Assert.Equal(1, entries);
        Assert.Equal(0x0112, (tiff[10] << 8) | tiff[11]);

        return (tiff[18] << 8) | tiff[19];
    }

    [Fact]
    public async Task Jpeg_LosesTheExifGpsXmpIptcCommentAndTheDataAfterTheImage()
    {
        var output = await StripAsync(ImageFixtures.Jpeg(), Jpeg);

        Assert.False(ImageFixtures.Contains(output, ImageFixtures.Sentinel));
        Assert.False(ImageFixtures.Contains(output, "Photoshop"));
        Assert.False(ImageFixtures.Contains(output, "MPF"));
        Assert.False(ImageFixtures.Contains(output, [0xCA, 0xFE, 0xBA, 0xBE]));
        Assert.False(ImageFixtures.Contains(output, [0xBE, 0xBA, 0xFE, 0xCA]));
        Assert.Equal([0xFF, 0xD9], output[^2..]);
    }

    [Fact]
    public async Task Jpeg_KeepsWhatRendersIt_AndTheScanByteForByte()
    {
        var output = await StripAsync(ImageFixtures.Jpeg(), Jpeg);

        Assert.Equal<byte>(
            [0xE1, 0xE0, 0xE2, 0xEE, 0xDB, 0xC0, 0xC4, 0xDA],
            JpegSegments(output).Select(x => x.Marker).ToArray());
        Assert.True(ImageFixtures.Contains(output, "ICC_PROFILE"));
        Assert.True(ImageFixtures.Contains(output, "Adobe"));
        Assert.True(ImageFixtures.Contains(output, ImageFixtures.JpegScan));
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(6, true)]
    [InlineData(8, true)]
    [InlineData(6, false)]
    [InlineData(3, false)]
    public async Task Jpeg_KeepsTheOrientation_AsTheOnlyExifTag(int orientation, bool littleEndian)
    {
        var output = await StripAsync(ImageFixtures.Jpeg(orientation, littleEndian), Jpeg);

        Assert.Equal(orientation, ExifOrientation(output));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(9)]
    public async Task Jpeg_WithNoRotation_HasNoExifAtAll(int orientation)
    {
        var output = await StripAsync(ImageFixtures.Jpeg(orientation), Jpeg);

        Assert.DoesNotContain(JpegSegments(output), x => x.Marker == 0xE1);
    }

    [Fact]
    public async Task Jpeg_KeepsTheJfifHeader_WithoutItsThumbnail()
    {
        var output = await StripAsync(ImageFixtures.Jpeg(), Jpeg);

        var jfif = JpegSegments(output).Single(x => x.Marker == 0xE0).Payload;
        Assert.Equal(14, jfif.Length);
        Assert.Equal<byte>([0, 0], jfif[12..]);
    }

    [Fact]
    public async Task Jpeg_WhoseExifIsDamaged_LosesItAndKeepsTheImage()
    {
        var jpeg = ImageFixtures.Jpeg();
        // The offset of the first IFD (little-endian, 4 bytes after the TIFF header's "II*\0") points far outside the segment
        var tiffStart = jpeg.AsSpan().IndexOf("Exif\0\0"u8) + 6;
        jpeg[tiffStart + 4] = 0xFF;
        jpeg[tiffStart + 5] = 0xFF;
        jpeg[tiffStart + 6] = 0xFF;
        jpeg[tiffStart + 7] = 0x7F;

        var output = await StripAsync(jpeg, Jpeg);

        Assert.DoesNotContain(JpegSegments(output), x => x.Marker == 0xE1);
        Assert.True(ImageFixtures.Contains(output, ImageFixtures.JpegScan));
    }

    [Fact]
    public async Task Jpeg_WithFillBytesBeforeAMarker_IsStillRead()
    {
        var jpeg = ImageFixtures.Jpeg(withTrailingData: false);
        var withFill = jpeg[..2].Concat(new byte[] { 0xFF, 0xFF }).Concat(jpeg[2..]).ToArray();

        var output = await StripAsync(withFill, Jpeg);

        Assert.True(ImageFixtures.Contains(output, ImageFixtures.JpegScan));
    }

    [Fact]
    public async Task Jpeg_WithAnExifLengthLongerThanTheFile_IsRejected()
    {
        var jpeg = ImageFixtures.Jpeg();
        var exif = jpeg.AsSpan().IndexOf("Exif"u8) - 2;
        jpeg[exif] = 0xFF;
        jpeg[exif + 1] = 0xFF;

        await AssertRejectedAsync(jpeg, Jpeg);
    }

    [Fact]
    public async Task Jpeg_WithoutAScan_IsRejected()
    {
        byte[] jpeg = [0xFF, 0xD8, ..ImageFixtures.Segment(0xDB, [0, 1, 2]), 0xFF, 0xD9];

        await AssertRejectedAsync(jpeg, Jpeg);
    }

    [Fact]
    public async Task Jpeg_CutAnywhereBeforeItsEnd_IsRejected()
    {
        var jpeg = ImageFixtures.Jpeg(withTrailingData: false);

        for (var length = 0; length < jpeg.Length; length++)
        {
            await AssertRejectedAsync(jpeg[..length], Jpeg);
        }
    }

    // ----- PNG -----

    private static List<string> PngChunkTypes(byte[] png)
    {
        var types = new List<string>();
        var position = 8;
        while (position < png.Length)
        {
            var length = (png[position] << 24) | (png[position + 1] << 16) | (png[position + 2] << 8) | png[position + 3];
            types.Add(global::System.Text.Encoding.ASCII.GetString(png, position + 4, 4));
            position += 12 + length;
        }

        return types;
    }

    [Fact]
    public async Task Png_LosesTextExifTimeXmpAndTheDataAfterIend()
    {
        var output = await StripAsync(ImageFixtures.Png(), Png);

        Assert.False(ImageFixtures.Contains(output, ImageFixtures.Sentinel));
        Assert.Equal(["IHDR", "gAMA", "sRGB", "pHYs", "IDAT", "IEND"], PngChunkTypes(output));
    }

    [Fact]
    public async Task Png_WithoutIhdrFirst_IsRejected()
    {
        var png = ImageFixtures.Png();
        png[12] = (byte)'X';

        await AssertRejectedAsync(png, Png);
    }

    [Fact]
    public async Task Png_CutAnywhereBeforeItsEnd_IsRejected()
    {
        var png = ImageFixtures.Png(withTrailingData: false);

        for (var length = 0; length < png.Length; length++)
        {
            await AssertRejectedAsync(png[..length], Png);
        }
    }

    [Fact]
    public async Task Png_WithAChunkLengthLongerThanTheFile_IsRejected()
    {
        var png = ImageFixtures.Png();
        png[8] = 0xFF;

        await AssertRejectedAsync(png, Png);
    }

    // ----- WebP -----

    [Fact]
    public async Task Webp_LosesExifAndXmp_AndClearsTheirFlags()
    {
        var output = await StripAsync(ImageFixtures.Webp(), Webp);

        Assert.False(ImageFixtures.Contains(output, ImageFixtures.Sentinel));
        Assert.False(ImageFixtures.Contains(output, "EXIF"));
        Assert.False(ImageFixtures.Contains(output, "XMP "));
        Assert.True(ImageFixtures.Contains(output, "VP8L"));

        // The flags byte of the extended header: alpha stays, EXIF and XMP are gone
        var flags = output[output.AsSpan().IndexOf("VP8X"u8) + 8];
        Assert.Equal(0x10, flags);
    }

    [Fact]
    public async Task Webp_WritesTheRiffSizeAgain_AndKeepsTheOddChunkPadded()
    {
        var output = await StripAsync(ImageFixtures.Webp(), Webp);

        var declared = BitConverter.ToUInt32(output, 4);
        Assert.Equal((uint)output.Length - 8, declared);
        Assert.Equal(0, output.Length % 2);
    }

    [Fact]
    public async Task Webp_CutAnywhereBeforeItsEnd_IsRejected()
    {
        var webp = ImageFixtures.Webp();

        for (var length = 0; length < webp.Length; length++)
        {
            await AssertRejectedAsync(webp[..length], Webp);
        }
    }

    [Fact]
    public async Task Webp_WithAChunkLongerThanTheFile_IsRejected()
    {
        var webp = ImageFixtures.Webp();
        webp[16] = 0xFF;
        webp[19] = 0x7F;

        await AssertRejectedAsync(webp, Webp);
    }

    // ----- GIF -----

    [Fact]
    public async Task Gif_LosesTheCommentXmpAndTheDataAfterTheTrailer_ButKeepsTheLoopAndTheFrame()
    {
        var output = await StripAsync(ImageFixtures.Gif(), Gif);

        Assert.False(ImageFixtures.Contains(output, ImageFixtures.Sentinel));
        Assert.False(ImageFixtures.Contains(output, "XMP DataXMP"));
        Assert.True(ImageFixtures.Contains(output, "NETSCAPE2.0"));
        Assert.True(ImageFixtures.Contains(output, [0x21, 0xF9, 4, 0, 10, 0, 0, 0]));
        Assert.True(ImageFixtures.Contains(output, [0x44, 0x01]));
        Assert.Equal(0x3B, output[^1]);
    }

    [Fact]
    public async Task Gif_CutAnywhereBeforeItsTrailer_IsRejected()
    {
        var gif = ImageFixtures.Gif(withTrailingData: false);

        for (var length = 0; length < gif.Length; length++)
        {
            await AssertRejectedAsync(gif[..length], Gif);
        }
    }

    // ----- All formats -----

    public static TheoryData<string, byte[]> Samples => new()
    {
        { ".jpg", ImageFixtures.Jpeg() },
        { ".jpg", ImageFixtures.Jpeg(orientation: 1) },
        { ".png", ImageFixtures.Png() },
        { ".webp", ImageFixtures.Webp() },
        { ".gif", ImageFixtures.Gif() }
    };

    private static ImageFormat FormatOf(string extension)
    {
        return new[] { Jpeg, Png, Gif, Webp }.Single(x => x.Extension == extension);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public async Task Stripping_AnImageThatHasNothingToStrip_ChangesNothing(string extension, byte[] sample)
    {
        var format = FormatOf(extension);
        var once = await StripAsync(sample, format);

        var twice = await StripAsync(once, format);

        Assert.Equal(once, twice);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Stripping_AnyMutationOfAnImage_NeverThrows(string extension, byte[] sample)
    {
        var stripper = Strippers.Single(x => x.Extension == FormatOf(extension).Extension);
        var random = new Random(2137);

        for (var i = 0; i < 2000; i++)
        {
            var mutated = (byte[])sample.Clone();
            for (var changes = random.Next(1, 4); changes > 0; changes--)
            {
                mutated[random.Next(mutated.Length)] = (byte)random.Next(256);
            }

            stripper.Strip(mutated);
        }
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".png")]
    [InlineData(".webp")]
    [InlineData(".gif")]
    public async Task AFileThatOnlyStartsLikeTheFormat_IsRejected(string extension)
    {
        byte[] noise = [0xFF, 0xD8, 0xFF, 0x89, 0x50, 0x4E, 0x47, 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 2, 3, 4];

        await AssertRejectedAsync(noise, FormatOf(extension));
    }

    [Fact]
    public async Task AFormatWithoutAStripper_IsRejected()
    {
        await AssertRejectedAsync(ImageFixtures.Jpeg(), new ImageFormat(".bmp", "image/bmp"));
    }

    [Fact]
    public async Task AStreamLongerThanTheLimit_IsRejected()
    {
        var tooLong = new byte[UploadedImageRules.MaxFileBytes + 1];

        await AssertRejectedAsync(tooLong, Jpeg);
    }
}
