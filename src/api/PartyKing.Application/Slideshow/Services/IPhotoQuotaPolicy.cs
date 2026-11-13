using ErrorOr;
using PartyKing.Application.Slideshow.Models;

namespace PartyKing.Application.Slideshow.Services;

/// <summary>What a session may hold of uploaded photos. An ordinary session has no limit of its own.</summary>
public interface IPhotoQuotaPolicy
{
    /// <summary>Whether <paramref name="files"/> may be added to the photos stored in <paramref name="sessionDirectory"/>.</summary>
    ErrorOr<Success> Check(string sessionDirectory, IReadOnlyList<UploadedFile> files);
}

internal class UnlimitedPhotoQuotaPolicy : IPhotoQuotaPolicy
{
    public ErrorOr<Success> Check(string sessionDirectory, IReadOnlyList<UploadedFile> files)
    {
        return Result.Success;
    }
}
