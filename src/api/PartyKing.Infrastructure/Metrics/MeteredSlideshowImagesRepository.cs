using PartyKing.Application.Ports;
using PartyKing.Domain.Entities.Slideshow;
using PartyKing.Domain.Repositories;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Infrastructure.Metrics;

internal sealed class MeteredSlideshowImagesRepository
    : MeteredRepository, ISlideshowImagesRepository, ISlideshowSettingsRepository
{
    private readonly ISlideshowImagesRepository _images;
    private readonly ISlideshowSettingsRepository _settings;

    public MeteredSlideshowImagesRepository(
        ISlideshowImagesRepository images,
        ISlideshowSettingsRepository settings,
        RepositoryMetrics metrics)
        : base(nameof(SlideshowImagesRepository), metrics)
    {
        _images = images;
        _settings = settings;
    }

    // Startup and file-system work, not database calls
    public Task InitializeAsync(string rootPath, string uploadedPath, CancellationToken cancellationToken)
    {
        return _images.InitializeAsync(rootPath, uploadedPath, cancellationToken);
    }

    public Task DeleteSessionFilesAsync(string sessionDirectory)
    {
        return _images.DeleteSessionFilesAsync(sessionDirectory);
    }

    public int CountSessionPhotos(string sessionDirectory)
    {
        return _images.CountSessionPhotos(sessionDirectory);
    }

    public Task<string?> CreateSessionArchiveAsync(string sessionDirectory, CancellationToken cancellationToken)
    {
        return _images.CreateSessionArchiveAsync(sessionDirectory, cancellationToken);
    }

    public Task<SlideshowSettings?> GetSettingsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetSettingsAsync), () => _settings.GetSettingsAsync(sessionId, cancellationToken));
    }

    public Task<SlideshowSettingsFailure> ChangeAsync(
        Guid sessionId,
        bool autoRepeat,
        bool startFromBeginning,
        TimeSpan? slideTime,
        TimeSpan defaultSlideTime,
        CancellationToken cancellationToken)
    {
        return MeasureAsync(
            nameof(ChangeAsync),
            () => _settings.ChangeAsync(
                sessionId,
                autoRepeat,
                startFromBeginning,
                slideTime,
                defaultSlideTime,
                cancellationToken));
    }

    public Task AddSlideshowImageAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(AddSlideshowImageAsync), () => _images.AddSlideshowImageAsync(slideshowImage, cancellationToken));
    }

    public Task<SlideshowImageReadResult> GetSlideshowImageAsync(
        Guid sessionId,
        Guid? currentId,
        SlideshowSettings settings,
        CancellationToken cancellationToken)
    {
        return MeasureAsync(
            nameof(GetSlideshowImageAsync),
            () => _images.GetSlideshowImageAsync(sessionId, currentId, settings, cancellationToken));
    }

    public Task DeleteImageAsync(SlideshowImageReadResult image, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(DeleteImageAsync), () => _images.DeleteImageAsync(image, cancellationToken));
    }
}
