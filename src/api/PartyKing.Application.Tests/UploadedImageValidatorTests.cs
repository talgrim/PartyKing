using PartyKing.Application.Slideshow.Services;
using PartyKing.Domain.Entities.Slideshow;

namespace PartyKing.Application.Tests;

/// <summary>The reading and the wording of the upload check; the rules themselves are <c>UploadedImageRulesTests</c>.</summary>
public class UploadedImageValidatorTests
{
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D];

    private static Task<ImageFormat> Validate(string fileName, byte[] content, long? length = null)
    {
        return UploadedImageValidator.ValidateAsync(
            fileName,
            length ?? content.Length,
            new MemoryStream(content),
            CancellationToken.None);
    }

    [Fact]
    public async Task Jpeg_IsAccepted()
    {
        Assert.Equal(new ImageFormat(".jpg", "image/jpeg"), await Validate("photo.JPEG", JpegBytes));
    }

    [Fact]
    public async Task StoredExtension_FollowsTheContent_NotTheClientsName()
    {
        // A real PNG that the client named .jpg is stored as .png, so the served content type matches the bytes
        var result = await Validate("holiday.jpg", PngBytes);

        Assert.Equal(".png", result.Extension);
    }

    [Fact]
    public async Task DisallowedExtension_IsRejectedWithItsMessage()
    {
        var error = await Assert.ThrowsAsync<InvalidUploadException>(() => Validate("dir/x.html", JpegBytes));

        Assert.Equal("'x.html' is not supported. Allowed formats: jpg, jpeg, png, gif, webp.", error.Message);
    }

    [Fact]
    public async Task EmptyFile_IsRejectedWithItsMessage()
    {
        var error = await Assert.ThrowsAsync<InvalidUploadException>(() => Validate("a.jpg", [], length: 0));

        Assert.Equal("'a.jpg' must be between 1 byte and 20 MB.", error.Message);
    }

    [Fact]
    public async Task HtmlDisguisedAsImage_IsRejectedWithItsMessage()
    {
        var html = "<html><script>alert(1)</script></html>"u8.ToArray();

        var error = await Assert.ThrowsAsync<InvalidUploadException>(() => Validate("evil.png", html));

        Assert.Equal("'evil.png' is not a valid image.", error.Message);
    }

    [Fact]
    public async Task Stream_IsRewoundForTheCaller()
    {
        var stream = new MemoryStream(JpegBytes);

        await UploadedImageValidator.ValidateAsync("a.jpg", JpegBytes.Length, stream, CancellationToken.None);

        Assert.Equal(0, stream.Position);
    }
}
