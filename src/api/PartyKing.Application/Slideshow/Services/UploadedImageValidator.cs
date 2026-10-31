namespace PartyKing.Application.Slideshow.Services;

internal record ValidatedImage(string Extension, string ContentType);

/// <summary>
/// Accepts only jpg/jpeg/png/gif/webp, judged by the file's extension and its magic bytes.
/// The client's content type is ignored: the stored extension and content type come from the detected format.
/// </summary>
internal static class UploadedImageValidator
{
    public const long MaxFileBytes = 20L * 1024 * 1024;

    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    private static readonly ValidatedImage Jpeg = new(".jpg", "image/jpeg");
    private static readonly ValidatedImage Png = new(".png", "image/png");
    private static readonly ValidatedImage Gif = new(".gif", "image/gif");
    private static readonly ValidatedImage Webp = new(".webp", "image/webp");

    public static async Task<ValidatedImage> ValidateAsync(
        string fileName,
        long length,
        Stream content,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidUploadException(
                $"'{Path.GetFileName(fileName)}' is not supported. Allowed formats: jpg, jpeg, png, gif, webp.");
        }

        if (length is <= 0 or > MaxFileBytes)
        {
            throw new InvalidUploadException(
                $"'{Path.GetFileName(fileName)}' must be between 1 byte and {MaxFileBytes / 1024 / 1024} MB.");
        }

        var header = new byte[12];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        content.Seek(0, SeekOrigin.Begin);

        return Detect(header.AsSpan(0, read))
               ?? throw new InvalidUploadException($"'{Path.GetFileName(fileName)}' is not a valid image.");
    }

    private static ValidatedImage? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
        {
            return Jpeg;
        }

        if (header.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return Png;
        }

        if (header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8))
        {
            return Gif;
        }

        // RIFF....WEBP
        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return Webp;
        }

        return null;
    }
}
