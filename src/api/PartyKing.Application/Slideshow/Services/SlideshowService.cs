using System.Collections.Concurrent;
using PartyKing.Application.Ports;
using PartyKing.Domain.Repositories;
using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Mappings;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Slideshow;
using PartyKing.Domain.Entities.Slideshow;

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

    /// <summary>
    /// The slide on display. While a viewer watches the session the server timer rotates it; otherwise the first
    /// read after the expiration does, so clients without a live connection still see the slideshow advance.
    /// </summary>
    Task<SlideshowImageDto?> GetImageAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Starts the server timer for the session (if it was idle) on behalf of a displaying connection.</summary>
    Task WatchAsync(Guid sessionId, string connectionId, CancellationToken cancellationToken);

    /// <summary>Stops the connection watching; the timer stops with the last watcher.</summary>
    void Unwatch(string connectionId);

    /// <summary>Drops the slideshow state and the uploaded files of a deleted session.</summary>
    Task DeleteSessionAsync(Guid sessionId);
}

internal class SlideshowService : ISlideshowService
{
    private static readonly TimeSpan MinSlideTime = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxSlideTime = TimeSpan.FromHours(1);

    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISlideshowImagesRepository _imagesRepository;
    private readonly ISlideshowSettingsRepository _settingsRepository;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly SlideshowConfiguration _slideshowConfiguration;
    private readonly ISlideshowWatchers _watchers;
    private readonly ISlideshowScheduler _scheduler;
    private readonly ISessionMusicNotifier _notifier;

    private readonly ConcurrentDictionary<Guid, SessionSlide> _slides = new();

    public SlideshowService(
        IDateTimeProvider dateTimeProvider,
        ISlideshowImagesRepository imagesRepository,
        ISlideshowSettingsRepository settingsRepository,
        ISessionsRepository sessionsRepository,
        IOptions<SlideshowConfiguration> slideshowSettingsOptions,
        ISlideshowWatchers watchers,
        ISlideshowScheduler scheduler,
        ISessionMusicNotifier notifier)
    {
        _dateTimeProvider = dateTimeProvider;
        _imagesRepository = imagesRepository;
        _settingsRepository = settingsRepository;
        _sessionsRepository = sessionsRepository;
        _slideshowConfiguration = slideshowSettingsOptions.Value;
        _watchers = watchers;
        _scheduler = scheduler;
        _notifier = notifier;
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
            return SessionErrors.NotFound();
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
            return SessionErrors.NotFound();
        }

        await _settingsRepository.SaveSettingsAsync(
            settings.ToDomain(sessionId, _slideshowConfiguration.SlideTime),
            cancellationToken);

        await RestartSlideAsync(sessionId, cancellationToken);

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

        await ShowFirstUploadAsync(sessionId, cancellationToken);
    }

    public async Task<SlideshowImageDto?> GetImageAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var slide = _slides.GetOrAdd(sessionId, _ => new SessionSlide());

        // One caller rotates the image at a time, so two of them can't skip or delete the same image twice
        await slide.Lock.WaitAsync(cancellationToken);
        try
        {
            // A watched session is rotated by the timer; reading it must not race with that
            if (!slide.Expiration.HasValue || (IsExpired(slide) && !_watchers.IsWatched(sessionId)))
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

    public async Task WatchAsync(Guid sessionId, string connectionId, CancellationToken cancellationToken)
    {
        _watchers.Watch(sessionId, connectionId);

        var slide = _slides.GetOrAdd(sessionId, _ => new SessionSlide());
        var changed = false;

        await slide.Lock.WaitAsync(cancellationToken);
        try
        {
            // Nobody rotated the slide while the session was unwatched: catch up once, then let the timer take over
            if (IsExpired(slide))
            {
                changed = await RotateAsync(sessionId, slide, cancellationToken);
            }

            Arm(sessionId, slide);
        }
        finally
        {
            slide.Lock.Release();
        }

        if (changed)
        {
            await _notifier.SlideshowChangedAsync(sessionId, cancellationToken);
        }
    }

    public void Unwatch(string connectionId)
    {
        if (_watchers.Unwatch(connectionId) is { Count: 0 } left)
        {
            _scheduler.Cancel(left.SessionId);
        }
    }

    public async Task DeleteSessionAsync(Guid sessionId)
    {
        _slides.TryRemove(sessionId, out _);
        _scheduler.Cancel(sessionId);

        await _imagesRepository.DeleteSessionFilesAsync(SessionDirectory(sessionId));
    }

    /// <summary>Moves to the next image. Returns whether the image on display is a different one.</summary>
    private async Task<bool> RotateAsync(Guid sessionId, SessionSlide slide, CancellationToken cancellationToken)
    {
        var previousUrl = slide.Current?.Data?.ImageUrl;

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

        return slide.Current?.Data?.ImageUrl != previousUrl;
    }

    private async Task OnTimerAsync(Guid sessionId)
    {
        if (!_watchers.IsWatched(sessionId) || !_slides.TryGetValue(sessionId, out var slide))
        {
            return;
        }

        var changed = false;

        await slide.Lock.WaitAsync();
        try
        {
            // A settings change or an upload may have moved the expiration since this timer was armed
            if (IsExpired(slide))
            {
                changed = await RotateAsync(sessionId, slide, CancellationToken.None);
            }

            Arm(sessionId, slide);
        }
        finally
        {
            slide.Lock.Release();
        }

        if (changed)
        {
            await _notifier.SlideshowChangedAsync(sessionId, CancellationToken.None);
        }
    }

    /// <summary>A new slide time applies to the slide on display right away, counted from now.</summary>
    private async Task RestartSlideAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!_slides.TryGetValue(sessionId, out var slide))
        {
            return;
        }

        await slide.Lock.WaitAsync(cancellationToken);
        try
        {
            if (slide.Expiration.HasValue)
            {
                var settings = await LoadSettingsAsync(sessionId, cancellationToken);
                slide.Expiration = _dateTimeProvider.UtcNow.Add(settings.SlideTime);
                Arm(sessionId, slide);
            }
        }
        finally
        {
            slide.Lock.Release();
        }
    }

    /// <summary>An empty slideshow shows the first uploaded photo at once instead of after its empty slide.</summary>
    private async Task ShowFirstUploadAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!_slides.TryGetValue(sessionId, out var slide))
        {
            return;
        }

        var changed = false;

        await slide.Lock.WaitAsync(cancellationToken);
        try
        {
            if (slide.Expiration.HasValue && slide.Current?.Data is null)
            {
                changed = await RotateAsync(sessionId, slide, cancellationToken);
                Arm(sessionId, slide);
            }
        }
        finally
        {
            slide.Lock.Release();
        }

        if (changed)
        {
            await _notifier.SlideshowChangedAsync(sessionId, cancellationToken);
        }
    }

    private void Arm(Guid sessionId, SessionSlide slide)
    {
        if (slide.Expiration is { } expiration && _watchers.IsWatched(sessionId))
        {
            _scheduler.Schedule(sessionId, expiration, () => OnTimerAsync(sessionId));
        }
    }

    private bool IsExpired(SessionSlide slide)
    {
        return !slide.Expiration.HasValue || slide.Expiration < _dateTimeProvider.UtcNow;
    }

    private async Task<SlideshowSettings> LoadSettingsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return await _settingsRepository.GetSettingsAsync(sessionId, cancellationToken)
               ?? new SlideshowSettings
               {
                   SessionId = sessionId,
                   AutoRepeat = true,
                   SlideTime = _slideshowConfiguration.SlideTime,
                   StartFromBeginning = true
               };
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


    private class SessionSlide
    {
        public SemaphoreSlim Lock { get; } = new(1, 1);
        public SlideshowImageReadResult? Current { get; set; }
        public DateTimeOffset? Expiration { get; set; }
    }
}
