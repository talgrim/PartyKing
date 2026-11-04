using PartyKing.Domain.Entities.Slideshow;

namespace PartyKing.Domain.Repositories;

public interface ISlideshowSettingsRepository
{
    /// <summary>The session's settings, or null when it never changed them.</summary>
    Task<SlideshowSettings?> GetSettingsAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the settings through <see cref="SlideshowSettings.Change"/>, creating the row when the session has none (two
    /// saves racing for a new session both succeed, the later one wins), and dispatches
    /// <c>SlideshowSettingsChanged</c> once it is committed. A refused slide time saves and dispatches nothing.
    /// </summary>
    Task<SlideshowSettingsFailure> ChangeAsync(
        Guid sessionId,
        bool autoRepeat,
        bool startFromBeginning,
        TimeSpan? slideTime,
        TimeSpan defaultSlideTime,
        CancellationToken cancellationToken);
}
