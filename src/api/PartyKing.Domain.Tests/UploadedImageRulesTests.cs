using PartyKing.Domain.Entities.Slideshow;

namespace PartyKing.Domain.Tests;

public class UploadedImageRulesTests
{
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D];
    private static readonly byte[] WebpBytes = "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();

    [Fact]
    public void Detect_Jpeg_IsStoredAsJpg()
    {
        Assert.Equal(new ImageFormat(".jpg", "image/jpeg"), UploadedImageRules.Detect(JpegBytes));
    }

    [Fact]
    public void Detect_Png_IsStoredAsPng()
    {
        Assert.Equal(new ImageFormat(".png", "image/png"), UploadedImageRules.Detect(PngBytes));
    }

    [Theory]
    [InlineData("GIF87a\0\0\0\0\0\0")]
    [InlineData("GIF89a\0\0\0\0\0\0")]
    public void Detect_Gif_IsStoredAsGif(string header)
    {
        Assert.Equal(new ImageFormat(".gif", "image/gif"), UploadedImageRules.Detect(System.Text.Encoding.ASCII.GetBytes(header)));
    }

    [Fact]
    public void Detect_Webp_IsStoredAsWebp()
    {
        Assert.Equal(new ImageFormat(".webp", "image/webp"), UploadedImageRules.Detect(WebpBytes));
    }

    [Fact]
    public void Detect_HtmlDisguisedAsImage_IsNone()
    {
        Assert.Null(UploadedImageRules.Detect("<html><script>alert(1)</script></html>"u8));
    }

    [Fact]
    public void Detect_RiffThatIsNotWebp_IsNone()
    {
        Assert.Null(UploadedImageRules.Detect("RIFF\0\0\0\0WAVEfmt "u8));
    }

    [Fact]
    public void Detect_WithoutBytes_IsNone()
    {
        Assert.Null(UploadedImageRules.Detect([]));
    }

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.JPEG")]
    [InlineData("a.png")]
    [InlineData("a.gif")]
    [InlineData("a.webp")]
    public void CheckFile_AnAllowedExtension_Passes(string fileName)
    {
        Assert.Equal(UploadedImageFailure.None, UploadedImageRules.CheckFile(fileName, 100));
    }

    [Theory]
    [InlineData("x.html")]
    [InlineData("x.svg")]
    [InlineData("x.heic")]
    [InlineData("x.exe")]
    [InlineData("x")]
    [InlineData("x.jpg.html")]
    public void CheckFile_AnotherExtension_IsUnsupported(string fileName)
    {
        Assert.Equal(UploadedImageFailure.UnsupportedExtension, UploadedImageRules.CheckFile(fileName, 100));
    }

    [Fact]
    public void CheckFile_EmptyFile_HasAnInvalidSize()
    {
        Assert.Equal(UploadedImageFailure.InvalidSize, UploadedImageRules.CheckFile("a.jpg", 0));
    }

    [Fact]
    public void CheckFile_OverTheLimit_HasAnInvalidSize()
    {
        Assert.Equal(UploadedImageFailure.InvalidSize, UploadedImageRules.CheckFile("a.jpg", UploadedImageRules.MaxFileBytes + 1));
    }

    [Fact]
    public void CheckFile_AtTheLimit_Passes()
    {
        Assert.Equal(UploadedImageFailure.None, UploadedImageRules.CheckFile("a.jpg", UploadedImageRules.MaxFileBytes));
    }

    [Fact]
    public void CheckFile_JudgesTheExtensionBeforeTheSize()
    {
        Assert.Equal(UploadedImageFailure.UnsupportedExtension, UploadedImageRules.CheckFile("a.exe", 0));
    }
}
