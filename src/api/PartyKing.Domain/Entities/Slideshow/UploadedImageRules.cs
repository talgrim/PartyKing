namespace PartyKing.Domain.Entities.Slideshow;

/// <summary>An image format the slideshow accepts: the extension and content type that are stored for it.</summary>
public sealed record ImageFormat(string Extension, string ContentType);

/// <summary>Why a file is not accepted as a photo.</summary>
public enum UploadedImageFailure
{
    None,
    UnsupportedExtension,
    InvalidSize,
    NotAnImage
}

/// <summary>
/// What an upload has to be to become a photo: jpg/jpeg/png/gif/webp by its name, at most <see cref="MaxFileBytes"/>, and
/// really that format by its magic bytes. The client's content type is never consulted: the stored extension and content
/// type come from the detected format. The caller reads the first <see cref="HeaderLength"/> bytes.
/// </summary>
public static class UploadedImageRules
{
    public const long MaxFileBytes = 20L * 1024 * 1024;

    /// <summary>How many leading bytes <see cref="Detect"/> needs.</summary>
    public const int HeaderLength = 12;

    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    private static readonly ImageFormat Jpeg = new(".jpg", "image/jpeg");
    private static readonly ImageFormat Png = new(".png", "image/png");
    private static readonly ImageFormat Gif = new(".gif", "image/gif");
    private static readonly ImageFormat Webp = new(".webp", "image/webp");

    /// <summary>The checks that need no content: the file's extension, then its size.</summary>
    public static UploadedImageFailure CheckFile(string fileName, long length)
    {
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return UploadedImageFailure.UnsupportedExtension;
        }

        if (length is <= 0 or > MaxFileBytes)
        {
            return UploadedImageFailure.InvalidSize;
        }

        return UploadedImageFailure.None;
    }

    /// <summary>The format the leading bytes of a file say it is; null when it is none of the accepted ones.</summary>
    public static ImageFormat? Detect(ReadOnlySpan<byte> header)
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
