using PartyKing.Domain.Entities.Slideshow;

namespace PartyKing.Application.Slideshow.Services;

/// <summary>
/// Reads an upload and applies <see cref="UploadedImageRules"/> to it; the messages are the ones the client sees. The
/// stream is read for its first bytes and rewound.
/// </summary>
internal static class UploadedImageValidator
{
    public static async Task<ImageFormat> ValidateAsync(
        string fileName,
        long length,
        Stream content,
        CancellationToken cancellationToken)
    {
        var name = Path.GetFileName(fileName);

        var failure = UploadedImageRules.CheckFile(fileName, length);
        if (failure != UploadedImageFailure.None)
        {
            throw new InvalidUploadException(Describe(failure, name));
        }

        var header = new byte[UploadedImageRules.HeaderLength];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        content.Seek(0, SeekOrigin.Begin);

        return UploadedImageRules.Detect(header.AsSpan(0, read))
               ?? throw new InvalidUploadException(Describe(UploadedImageFailure.NotAnImage, name));
    }

    private static string Describe(UploadedImageFailure failure, string name)
    {
        return failure switch
        {
            UploadedImageFailure.UnsupportedExtension =>
                $"'{name}' is not supported. Allowed formats: jpg, jpeg, png, gif, webp.",
            UploadedImageFailure.InvalidSize =>
                $"'{name}' must be between 1 byte and {UploadedImageRules.MaxFileBytes / 1024 / 1024} MB.",
            _ => $"'{name}' is not a valid image."
        };
    }
}
