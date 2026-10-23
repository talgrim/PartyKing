using PartyKing.Application.Slideshow.Services;

namespace PartyKing.Application.Tests;

public class UploadedImageValidatorTests
{
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D];
    private static readonly byte[] WebpBytes = "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();

    private static Task<ValidatedImage> Validate(string fileName, byte[] content, long? length = null) =>
        UploadedImageValidator.ValidateAsync(
            fileName,
            length ?? content.Length,
            new MemoryStream(content),
            CancellationToken.None);

    [Theory]
    [InlineData("photo.jpg", ".jpg", "image/jpeg")]
    [InlineData("photo.JPEG", ".jpg", "image/jpeg")]
    public async Task Jpeg_IsAccepted(string fileName, string extension, string contentType)
    {
        var result = await Validate(fileName, JpegBytes);

        Assert.Equal(new ValidatedImage(extension, contentType), result);
    }

    [Fact]
    public async Task Png_IsAccepted()
    {
        Assert.Equal(new ValidatedImage(".png", "image/png"), await Validate("a.png", PngBytes));
    }

    [Fact]
    public async Task Gif_IsAccepted()
    {
        Assert.Equal(new ValidatedImage(".gif", "image/gif"), await Validate("a.gif", "GIF89a\0\0\0\0\0\0"u8.ToArray()));
    }

    [Fact]
    public async Task Webp_IsAccepted()
    {
        Assert.Equal(new ValidatedImage(".webp", "image/webp"), await Validate("a.webp", WebpBytes));
    }

    [Fact]
    public async Task StoredExtension_FollowsTheContent_NotTheClientsName()
    {
        // A real PNG that the client named .jpg is stored as .png, so the served content type matches the bytes
        var result = await Validate("holiday.jpg", PngBytes);

        Assert.Equal(".png", result.Extension);
    }

    [Theory]
    [InlineData("x.html")]
    [InlineData("x.svg")]
    [InlineData("x.heic")]
    [InlineData("x.exe")]
    [InlineData("x")]
    [InlineData("x.jpg.html")]
    public async Task DisallowedExtension_IsRejected(string fileName)
    {
        await Assert.ThrowsAsync<InvalidUploadException>(() => Validate(fileName, JpegBytes));
    }

    [Fact]
    public async Task HtmlDisguisedAsImage_IsRejected()
    {
        var html = "<html><script>alert(1)</script></html>"u8.ToArray();

        await Assert.ThrowsAsync<InvalidUploadException>(() => Validate("evil.png", html));
    }

    [Fact]
    public async Task RiffThatIsNotWebp_IsRejected()
    {
        var wav = "RIFF\0\0\0\0WAVEfmt "u8.ToArray();

        await Assert.ThrowsAsync<InvalidUploadException>(() => Validate("a.webp", wav));
    }

    [Fact]
    public async Task EmptyFile_IsRejected()
    {
        await Assert.ThrowsAsync<InvalidUploadException>(() => Validate("a.jpg", [], length: 0));
    }

    [Fact]
    public async Task FileOverTheLimit_IsRejected()
    {
        await Assert.ThrowsAsync<InvalidUploadException>(
            () => Validate("a.jpg", JpegBytes, length: UploadedImageValidator.MaxFileBytes + 1));
    }

    [Fact]
    public async Task FileAtTheLimit_IsAccepted()
    {
        var result = await Validate("a.jpg", JpegBytes, length: UploadedImageValidator.MaxFileBytes);

        Assert.Equal(".jpg", result.Extension);
    }

    [Fact]
    public async Task Stream_IsRewoundForTheCaller()
    {
        var stream = new MemoryStream(JpegBytes);

        await UploadedImageValidator.ValidateAsync("a.jpg", JpegBytes.Length, stream, CancellationToken.None);

        Assert.Equal(0, stream.Position);
    }
}
