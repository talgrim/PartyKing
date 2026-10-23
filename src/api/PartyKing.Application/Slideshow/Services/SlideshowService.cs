using System.Collections.Concurrent;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Mappings;
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
    Task<ErrorOr<SlideshowSettingsDto>> GetSettingsAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<ErrorOr<Success>> UpdateSettingsAsync(
        Guid sessionId,
        SlideshowSettingsDto settings,
        CancellationToken cancellationToken);
    Task UploadImagesAsync(
        Guid sessionId,
        IFormFile[] files,
        bool deleteAfterPresentation,
        CancellationToken cancellationToken);
    Task<SlideshowImageDto?> GetImageAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Drops the slideshow state and the uploaded files of a deleted session.</summary>
    Task DeleteSessionAsync(Guid sessionId);
}

internal class SlideshowService : ISlideshowService
{
    private static readonly TimeSpan MinSlideTime = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxSlideTime = TimeSpan.FromHours(1);

    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISlideshowImagesRepository _imagesRepository;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly SlideshowConfiguration _slideshowConfiguration;

    private readonly ConcurrentDictionary<Guid, SessionSlide> _slides = new();

    public SlideshowService(
        IDateTimeProvider dateTimeProvider,
        ISlideshowImagesRepository imagesRepository,
        ISessionsRepository sessionsRepository,
        IOptions<SlideshowConfiguration> slideshowSettingsOptions)
    {
        _dateTimeProvider = dateTimeProvider;
        _imagesRepository = imagesRepository;
        _sessionsRepository = sessionsRepository;
        _slideshowConfiguration = slideshowSettingsOptions.Value;
    }

    public Task InitializeAsync(string rootPath, CancellationToken cancellationToken)
    {
        return _imagesRepository.InitializeAsync(
            rootPath,
            _slideshowConfiguration.UploadedPhotosDirectory,
            cancellationToken);
    }

    public async Task<ErrorOr<SlideshowSettingsDto>> GetSettingsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (await _sessionsRepository.GetAsync(sessionId, cancellationToken) is null)
        {
            return SessionNotFound();
        }

        return (await LoadSettingsAsync(sessionId, cancellationToken)).ToDto();
    }

    public async Task<ErrorOr<Success>> UpdateSettingsAsync(
        Guid sessionId,
        SlideshowSettingsDto settings,
        CancellationToken cancellationToken)
    {
        if (settings.SlideTime is { } slideTime && (slideTime < MinSlideTime || slideTime > MaxSlideTime))
        {
            return Error.Validation(description: "The slide time must be between 1 second and 1 hour");
        }

        if (await _sessionsRepository.GetAsync(sessionId, cancellationToken) is null)
        {
            return SessionNotFound();
        }

        await _imagesRepository.SaveSettingsAsync(
            settings.ToDomain(sessionId, _slideshowConfiguration.SlideTime),
            cancellationToken);

        return Result.Success;
    }

    public async Task UploadImagesAsync(
        Guid sessionId,
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
                await UploadImageAsync(sessionId, item.File, item.Image, deleteAfterPresentation, stoppingToken));
    }

    public async Task<SlideshowImageDto?> GetImageAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var slide = _slides.GetOrAdd(sessionId, _ => new SessionSlide());

        // One client rotates the image at a time, so two polls can't skip or delete the same image twice
        await slide.Lock.WaitAsync(cancellationToken);
        try
        {
            if (!slide.Expiration.HasValue || slide.Expiration < _dateTimeProvider.UtcNow)
            {
                await RotateAsync(sessionId, slide, cancellationToken);
            }

            // Nothing uploaded: there is nothing to show
            if (slide.Current?.Data is not { } data || !slide.Expiration.HasValue)
            {
                return null;
            }

            return new SlideshowImageDto
            {
                FileName = data.ImageUrl,
                ExpirationDate = slide.Expiration.Value
            };
        }
        finally
        {
            slide.Lock.Release();
        }
    }

    public async Task DeleteSessionAsync(Guid sessionId)
    {
        _slides.TryRemove(sessionId, out _);

        await _imagesRepository.DeleteSessionFilesAsync(SessionDirectory(sessionId));
    }

    private async Task RotateAsync(Guid sessionId, SessionSlide slide, CancellationToken cancellationToken)
    {
        if (slide.Current?.Data is { DeleteAfterPresentation: true })
        {
            await _imagesRepository.DeleteImageAsync(slide.Current, cancellationToken);
        }

        var settings = await LoadSettingsAsync(sessionId, cancellationToken);

        slide.Expiration = _dateTimeProvider.UtcNow.Add(settings.SlideTime);

        // Kept even without image data, so the next lookup continues after this id and a deleted image is not shown again
        slide.Current = await _imagesRepository.GetSlideshowImageAsync(
            sessionId,
            slide.Current?.ImageId,
            settings,
            cancellationToken);
    }

    private async Task<SlideshowSettings> LoadSettingsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return await _imagesRepository.GetSettingsAsync(sessionId, cancellationToken)
            ?? new SlideshowSettings { SessionId = sessionId, SlideTime = _slideshowConfiguration.SlideTime };
    }

    private async Task UploadImageAsync(
        Guid sessionId,
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
                sessionId,
                image.Extension,
                image.ContentType,
                content,
                SessionDirectory(sessionId),
                deleteAfterPresentation);
        }
    }

    private string SessionDirectory(Guid sessionId) => $"{_slideshowConfiguration.UploadedPhotosDirectory}/{sessionId}";

    private static Error SessionNotFound() => Error.NotFound(description: "The session was not found");

    private class SessionSlide
    {
        public SemaphoreSlim Lock { get; } = new(1, 1);
        public SlideshowImageReadResult? Current { get; set; }
        public DateTimeOffset? Expiration { get; set; }
    }
}
