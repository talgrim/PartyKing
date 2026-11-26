using ErrorOr;
using PartyKing.Domain.Entities.Slideshow;

namespace PartyKing.Application.Slideshow.Helpers;

/// <summary>The reasons a photo is not stored; the descriptions are shown to the guest who uploaded it.</summary>
internal static class SlideshowErrors
{
    public const string InvalidUploadCode = "Slideshow.InvalidUpload";

    public static Error UnsupportedExtension(string name)
    {
        return InvalidUpload($"'{name}' is not supported. Allowed formats: jpg, jpeg, png, gif, webp.");
    }

    public static Error InvalidSize(string name)
    {
        return InvalidUpload($"'{name}' must be between 1 byte and {UploadedImageRules.MaxFileBytes / 1024 / 1024} MB.");
    }

    public static Error NotAnImage(string name)
    {
        return InvalidUpload($"'{name}' is not a valid image.");
    }

    /// <summary>The format is recognised, but nothing is registered that can take its metadata out.</summary>
    public static Error NotSupported(string name)
    {
        return InvalidUpload($"'{name}' is not supported.");
    }

    public static Error Damaged(string name)
    {
        return InvalidUpload($"'{name}' is damaged and could not be processed.");
    }

    private static Error InvalidUpload(string description)
    {
        return Error.Validation(code: InvalidUploadCode, description: description);
    }
}
