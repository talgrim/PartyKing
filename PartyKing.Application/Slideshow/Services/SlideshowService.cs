using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Slideshow;
using PartyKing.Domain.Models.Slideshow;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Application.Slideshow.Services;

public interface ISlideshowService
{
    bool IsInitialized();
    void UpdateSettings(bool autoRepeat, string rootPath, TimeSpan? slideTime = null);
    Task UploadImagesAsync(IFormFile[] files, bool deleteAfterPresentation, CancellationToken cancellationToken);
    Task<SlideshowImageDto?> GetImageAsync(CancellationToken cancellationToken);
}

public class SlideshowService : ISlideshowService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISlideshowImagesRepository _imagesRepository;
    private readonly SlideshowSettings _slideshowSettings;

    private TimeSpan _slideTime;

    private SlideshowImageReadResult? _currentImage;
    private DateTimeOffset? _currentImageExpiration;

    public SlideshowService(
        IDateTimeProvider dateTimeProvider,
        ISlideshowImagesRepository imagesRepository,
        IOptions<SlideshowSettings> slideshowSettingsOptions)
    {
        _dateTimeProvider = dateTimeProvider;
        _imagesRepository = imagesRepository;
        _slideshowSettings = slideshowSettingsOptions.Value;
        _slideTime = _slideshowSettings.SlideTime;
    }

    public bool IsInitialized()
    {
        return _imagesRepository.IsInitialized;
    }

    public void UpdateSettings(bool autoRepeat, string rootPath, TimeSpan? slideTime = null)
    {
        if (slideTime is not null)
        {
            _slideTime = slideTime.Value;
        }

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

    public async Task<SlideshowImageDto?> GetImageAsync(CancellationToken cancellationToken)
    {
        if (!_currentImageExpiration.HasValue || _currentImageExpiration < _dateTimeProvider.UtcNow)
        {
            if (_currentImage is not null && _currentImage.Data!.DeleteAfterPresentation)
            {
                await _imagesRepository.DeleteImageAsync(_currentImage, cancellationToken);
            }

            await RefreshCurrentImageAsync(cancellationToken);
        }

        return MapToContract();

        SlideshowImageDto MapToContract()
        {
            return new SlideshowImageDto
            {
                FileName = _currentImage!.Data!.ImageUrl,
                ExpirationDate = _currentImageExpiration!.Value
            };
        }
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

    private async Task RefreshCurrentImageAsync(CancellationToken cancellationToken)
    {
        _currentImageExpiration = _dateTimeProvider.UtcNow.Add(_slideTime);

        var newImage = await _imagesRepository.GetSlideshowImageAsync(_currentImage?.ImageId, cancellationToken);

        if (!newImage.IsOk)
        {
            return;
        }

        _currentImage = newImage;
    }
}