using PartyKing.Domain.Entities;
using PartyKing.Domain.Models.Slideshow;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Infrastructure.Metrics;

internal sealed class MeteredSlideshowImagesRepository : MeteredRepository, ISlideshowImagesRepository
{
    private readonly ISlideshowImagesRepository _inner;

    public MeteredSlideshowImagesRepository(ISlideshowImagesRepository inner, RepositoryMetrics metrics)
        : base(nameof(SlideshowImagesRepository), metrics)
    {
        _inner = inner;
    }

    // Startup and file-system work, not database calls
    public Task InitializeAsync(string rootPath, string uploadedPath, CancellationToken cancellationToken)
    {
        return _inner.InitializeAsync(rootPath, uploadedPath, cancellationToken);
    }

    public Task DeleteSessionFilesAsync(string sessionDirectory)
    {
        return _inner.DeleteSessionFilesAsync(sessionDirectory);
    }

    public Task<SlideshowSettings?> GetSettingsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetSettingsAsync), () => _inner.GetSettingsAsync(sessionId, cancellationToken));
    }

    public Task SaveSettingsAsync(SlideshowSettings settings, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(SaveSettingsAsync), () => _inner.SaveSettingsAsync(settings, cancellationToken));
    }

    public Task AddSlideshowImageAsync(SlideshowImageWriteModel slideshowImage, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(AddSlideshowImageAsync), () => _inner.AddSlideshowImageAsync(slideshowImage, cancellationToken));
    }

    public Task<SlideshowImageReadResult> GetSlideshowImageAsync(
        Guid sessionId,
        Guid? currentId,
        SlideshowSettings settings,
        CancellationToken cancellationToken)
    {
        return MeasureAsync(
            nameof(GetSlideshowImageAsync),
            () => _inner.GetSlideshowImageAsync(sessionId, currentId, settings, cancellationToken));
    }

    public Task DeleteImageAsync(SlideshowImageReadResult image, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(DeleteImageAsync), () => _inner.DeleteImageAsync(image, cancellationToken));
    }
}
