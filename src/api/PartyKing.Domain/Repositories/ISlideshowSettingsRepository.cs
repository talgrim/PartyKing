using PartyKing.Domain.Entities.Slideshow;

namespace PartyKing.Domain.Repositories;

public interface ISlideshowSettingsRepository
{
    /// <summary>The session's settings, or null when it never changed them.</summary>
    Task<SlideshowSettings?> GetSettingsAsync(Guid sessionId, CancellationToken cancellationToken);
    Task SaveSettingsAsync(SlideshowSettings settings, CancellationToken cancellationToken);
}
