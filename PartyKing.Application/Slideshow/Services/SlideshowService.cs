using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.System;
using PartyKing.Domain.Models.Slideshow;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Slideshow.Services;

public interface ISlideshowService
{
    bool IsInitialized();
    void UpdateSettings(bool autoRepeat, string rootPath);
    Task UploadImagesAsync(IFormFile[] files, bool deleteAfterPresentation, CancellationToken cancellationToken);
    Task<SlideshowImageReadModel?> GetImageAsync(CancellationToken cancellationToken);
}

public class SlideshowService : ISlideshowService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISlideshowImagesRepository _imagesRepository;
    private readonly SlideshowSettings _slideshowSettings;

    public SlideshowService(
        IDateTimeProvider dateTimeProvider,
        ISlideshowImagesRepository imagesRepository,
        IOptions<SlideshowSettings> slideshowSettingsOptions)
    {
        _dateTimeProvider = dateTimeProvider;
        _imagesRepository = imagesRepository;
        _slideshowSettings = slideshowSettingsOptions.Value;
    }

    public bool IsInitialized()
    {
        return _imagesRepository.IsInitialized;
    }

    public void UpdateSettings(bool autoRepeat, string rootPath)
    {
        _imagesRepository.UpdateSettings(autoRepeat, rootPath, _slideshowSettings.PlaceholderPhotosDirectory);
    }

    public async Task UploadImagesAsync(
        IFormFile[] files,
        bool deleteAfterPresentation,
        CancellationToken cancellationToken)
    {
        await Parallel.ForEachAsync(
            files,
            cancellationToken,
            async (file, stoppingToken) => await UploadImageAsync(file, deleteAfterPresentation, stoppingToken));
    }

    public Task<SlideshowImageReadModel?> GetImageAsync(CancellationToken cancellationToken)
    {
        return _imagesRepository.GetSlideshowImageAsync(cancellationToken);
    }

    private async Task UploadImageAsync(
        IFormFile file,
        bool deleteAfterPresentation,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();

        await _imagesRepository.AddSlideshowImageAsync(CreateModel(), cancellationToken);

        return;

        SlideshowImageWriteModel CreateModel()
        {
            return new SlideshowImageWriteModel(
                file.FileName,
                file.ContentType,
                content,
                _slideshowSettings.UploadedPhotosDirectory,
                deleteAfterPresentation);
        }
    }
}