using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Slideshow;
using PartyKing.Domain.Entities;
using PartyKing.Domain.Models.Slideshow;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Application.Slideshow.Services;

public interface ISlideshowService
{
    Task InitializeAsync(string rootPath, CancellationToken cancellationToken);
    Task UpdateSettingsAsync(SlideshowSettings settings, CancellationToken cancellationToken);
    Task UploadImagesAsync(IFormFile[] files, bool deleteAfterPresentation, CancellationToken cancellationToken);
    Task<SlideshowImageDto?> GetImageAsync(CancellationToken cancellationToken);
}

internal class SlideshowService : ISlideshowService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISlideshowImagesRepository _imagesRepository;
    private readonly SlideshowConfiguration _slideshowConfiguration;

    private SlideshowImageReadResult? _currentImage;
    private DateTimeOffset? _currentImageExpiration;

    public SlideshowService(
        IDateTimeProvider dateTimeProvider,
        ISlideshowImagesRepository imagesRepository,
        IOptions<SlideshowConfiguration> slideshowSettingsOptions)
    {
        _dateTimeProvider = dateTimeProvider;
        _imagesRepository = imagesRepository;
        _slideshowConfiguration = slideshowSettingsOptions.Value;
    }

    public Task InitializeAsync(string rootPath, CancellationToken cancellationToken)
    {
        return _imagesRepository.InitializeAsync(
            rootPath,
            _slideshowConfiguration.PlaceholderPhotosDirectory,
            _slideshowConfiguration.UploadedPhotosDirectory,
            cancellationToken);
    }

    public Task UpdateSettingsAsync(SlideshowSettings settings, CancellationToken cancellationToken)
    {
        return _imagesRepository.UpdateSettingsAsync(settings, cancellationToken);
    }

    public async Task UploadImagesAsync(
        IFormFile[] files,
        bool deleteAfterPresentation,
        CancellationToken cancellationToken)
    {
        // Validate everything first so a bad file doesn't leave the rest of the batch half stored
        var validated = new List<(IFormFile File, ValidatedImage Image)>();
        foreach (var file in files)
        {
            await using var content = file.OpenReadStream();
            var image = await UploadedImageValidator.ValidateAsync(file.FileName, file.Length, content, cancellationToken);
            validated.Add((file, image));
        }

        await Parallel.ForEachAsync(
            validated,
            cancellationToken,
            async (item, stoppingToken) =>
                await UploadImageAsync(item.File, item.Image, deleteAfterPresentation, stoppingToken));
    }

    public async Task<SlideshowImageDto?> GetImageAsync(CancellationToken cancellationToken)
    {
        if (!_currentImageExpiration.HasValue || _currentImageExpiration < _dateTimeProvider.UtcNow)
        {
            if (_currentImage?.Data is { DeleteAfterPresentation: true })
            {
                await _imagesRepository.DeleteImageAsync(_currentImage, cancellationToken);
            }

            await RefreshCurrentImageAsync(cancellationToken);
        }

        // Nothing uploaded and no placeholders: there is nothing to show
        if (_currentImage?.Data is null || !_currentImageExpiration.HasValue)
        {
            return null;
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
        ValidatedImage image,
        bool deleteAfterPresentation,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();

        await _imagesRepository.AddSlideshowImageAsync(CreateModel(), cancellationToken);

        return;

        SlideshowImageWriteModel CreateModel()
        {
            return new SlideshowImageWriteModel(
                image.Extension,
                image.ContentType,
                content,
                _slideshowConfiguration.UploadedPhotosDirectory,
                deleteAfterPresentation);
        }
    }

    private async Task RefreshCurrentImageAsync(CancellationToken cancellationToken)
    {
        _currentImageExpiration = _dateTimeProvider.UtcNow.Add(_imagesRepository.SlideTime);

        var newImage = await _imagesRepository.GetSlideshowImageAsync(_currentImage?.ImageId, cancellationToken);

        // Kept even without image data, so the next lookup continues after this id and a deleted image is not shown again
        _currentImage = newImage;
    }
}