using System.Collections.Concurrent;
using PartyKing.Application.Ports;
using PartyKing.Domain.Repositories;
using ErrorOr;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Mappings;
using PartyKing.Application.Realtime;
using PartyKing.Application.Slideshow.Models;
using PartyKing.Application.Sessions;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Slideshow;
using PartyKing.Domain.Entities.Slideshow;

namespace PartyKing.Application.Slideshow.Services;

public interface ISlideshowService
{
    Task InitializeAsync(string rootPath, CancellationToken cancellationToken);

    /// <summary>The session's slideshow settings. Host or admin only (Forbidden otherwise).</summary>
    Task<ErrorOr<SlideshowSettingsDto>> GetSettingsAsync(
        Guid sessionId,
        SessionCaller caller,
        CancellationToken cancellationToken);

    /// <summary>Host or admin only (Forbidden otherwise).</summary>
    Task<ErrorOr<Success>> UpdateSettingsAsync(
        Guid sessionId,
        SessionCaller caller,
        SlideshowSettingsDto settings,
        CancellationToken cancellationToken);
    Task UploadImagesAsync(
        Guid sessionId,
        IReadOnlyList<UploadedFile> files,
        bool deleteAfterPresentation,
        CancellationToken cancellationToken);

    /// <summary>
    /// The slide on display. While a viewer watches the session the server timer rotates it; otherwise the first
    /// read after the expiration does, so clients without a live connection still see the slideshow advance.
    /// </summary>
    Task<SlideshowImageDto?> GetImageAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Starts the server timer for the session (if it was idle) on behalf of a displaying connection. Host or admin
    /// only (Forbidden otherwise): nobody else displays the slideshow.
    /// </summary>
    Task<ErrorOr<Success>> WatchAsync(
        Guid sessionId,
        SessionCaller caller,
        string connectionId,
        CancellationToken cancellationToken);

    /// <summary>Stops the connection watching; the timer stops with the last watcher.</summary>
    void Unwatch(string connectionId);

    /// <summary>Drops the slideshow state and the uploaded files of a deleted session.</summary>
    Task DeleteSessionAsync(Guid sessionId);

    /// <summary>A new slide time applies to the slide on display right away, counted from now.</summary>
    Task RestartSlideAsync(Guid sessionId, CancellationToken cancellationToken);
}

internal class SlideshowService : ManagerGuardedService, ISlideshowService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISlideshowImagesRepository _imagesRepository;
    private readonly ISlideshowSettingsRepository _settingsRepository;
    private readonly SlideshowConfiguration _slideshowConfiguration;
    private readonly IPhotoQuotaPolicy _photoQuota;
    private readonly ISlideshowWatchers _watchers;
    private readonly ISlideshowScheduler _scheduler;
    private readonly ISlideshowNotifier _notifier;

    private readonly ConcurrentDictionary<Guid, SessionSlide> _slides = new();

    public SlideshowService(
        IDateTimeProvider dateTimeProvider,
        ISlideshowImagesRepository imagesRepository,
        ISlideshowSettingsRepository settingsRepository,
        ISessionAuthorizer sessionAuthorizer,
        IOptions<SlideshowConfiguration> slideshowSettingsOptions,
        ISlideshowWatchers watchers,
        ISlideshowScheduler scheduler,
        ISlideshowNotifier notifier,
        IPhotoQuotaPolicy photoQuota)
        : base(sessionAuthorizer)
    {
        _dateTimeProvider = dateTimeProvider;
        _imagesRepository = imagesRepository;
        _settingsRepository = settingsRepository;
        _slideshowConfiguration = slideshowSettingsOptions.Value;
        _photoQuota = photoQuota;
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

    public Task<ErrorOr<SlideshowSettingsDto>> GetSettingsAsync(
        Guid sessionId,
        SessionCaller caller,
        CancellationToken cancellationToken)
    {
        return RunAsManagerAsync<SlideshowSettingsDto>(
            sessionId,
            caller,
            async () => (await LoadSettingsAsync(sessionId, cancellationToken)).ToDto(),
            cancellationToken);
    }

    public Task<ErrorOr<Success>> UpdateSettingsAsync(
        Guid sessionId,
        SessionCaller caller,
        SlideshowSettingsDto settings,
        CancellationToken cancellationToken)
    {
        return RunAsManagerAsync(
            sessionId,
            caller,
            UpdateSettingsAsyncCore,
            cancellationToken);

        async Task<ErrorOr<Success>> UpdateSettingsAsyncCore()
        {
            // Refused before the row is read; the rule is the aggregate's
            var rangeFailure = SlideshowSettings.ValidateSlideTime(settings.SlideTime);
            if (rangeFailure != SlideshowSettingsFailure.None)
            {
                return ToError(rangeFailure);
            }

            // The slide on display is restarted by the handler of the SlideshowSettingsChanged event
            var failure = await _settingsRepository.ChangeAsync(
                sessionId,
                settings.AutoRepeat,
                settings.StartFromBeginning,
                settings.SlideTime,
                _slideshowConfiguration.SlideTime,
                cancellationToken);

            return failure == SlideshowSettingsFailure.None ? Result.Success : ToError(failure);
        }
    }

    private static Error ToError(SlideshowSettingsFailure failure)
    {
        return Error.Validation(description: "The slide time must be between 1 second and 1 hour");
    }

    public async Task UploadImagesAsync(
        Guid sessionId,
        IReadOnlyList<UploadedFile> files,
        bool deleteAfterPresentation,
        CancellationToken cancellationToken)
    {
        EnsureWithinQuota(sessionId, files);

        // Validate everything first so a bad file doesn't leave the rest of the batch half stored
        var validated = new List<(UploadedFile File, ImageFormat Image)>();
        foreach (var file in files)
        {
            await using var content = file.OpenRead();
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

    /// <summary>The upload endpoint answers a refusal like it answers an invalid file, with a bad request and its text.</summary>
    private void EnsureWithinQuota(Guid sessionId, IReadOnlyList<UploadedFile> files)
    {
        var quota = _photoQuota.Check(SessionDirectory(sessionId), files);
        if (quota.IsError)
        {
            throw new InvalidUploadException(quota.FirstError.Description);
        }
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

    public Task<ErrorOr<Success>> WatchAsync(
        Guid sessionId,
        SessionCaller caller,
        string connectionId,
        CancellationToken cancellationToken)
    {
        return RunAsManagerAsync(
            sessionId,
            caller,
            WatchAsyncCore,
            cancellationToken);

        async Task<ErrorOr<Success>> WatchAsyncCore()
        {
            await StartWatchingAsync(sessionId, connectionId, cancellationToken);

            return Result.Success;
        }
    }

    private async Task StartWatchingAsync(Guid sessionId, string connectionId, CancellationToken cancellationToken)
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
    public async Task RestartSlideAsync(Guid sessionId, CancellationToken cancellationToken)
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
               ?? SlideshowSettings.Defaults(sessionId, _slideshowConfiguration.SlideTime);
    }

    private async Task UploadImageAsync(
        Guid sessionId,
        UploadedFile file,
        ImageFormat image,
        bool deleteAfterPresentation,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenRead();

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
