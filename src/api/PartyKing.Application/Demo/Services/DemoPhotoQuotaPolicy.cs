using ErrorOr;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Demo.Helpers;
using PartyKing.Application.Ports;
using PartyKing.Application.Slideshow.Models;
using PartyKing.Application.Slideshow.Services;

namespace PartyKing.Application.Demo.Services;

/// <summary>
/// A public demo has no owner to pay for the disk, so it keeps a session to a few small photos. The count is read
/// before the files are written, so uploads racing each other can overshoot it by a batch.
/// </summary>
internal class DemoPhotoQuotaPolicy : IPhotoQuotaPolicy
{
    private readonly DemoConfiguration _configuration;
    private readonly ISlideshowImagesRepository _imagesRepository;

    public DemoPhotoQuotaPolicy(IOptions<DemoConfiguration> options, ISlideshowImagesRepository imagesRepository)
    {
        _configuration = options.Value;
        _imagesRepository = imagesRepository;
    }

    public ErrorOr<Success> Check(string sessionDirectory, IReadOnlyList<UploadedFile> files)
    {
        if (files.Any(file => file.Length > _configuration.MaxPhotoBytes))
        {
            return DemoErrors.PhotoTooLarge(_configuration.MaxPhotoBytes / (1024 * 1024));
        }

        var stored = _imagesRepository.CountSessionPhotos(sessionDirectory);

        return stored + files.Count > _configuration.MaxPhotosPerSession
            ? DemoErrors.TooManyPhotos(_configuration.MaxPhotosPerSession)
            : Result.Success;
    }
}
