namespace PartyKing.Application.Realtime;

/// <summary>Pushes slideshow changes to a session.</summary>
public interface ISlideshowNotifier
{
    /// <summary>The image on display in the session's slideshow changed; clients fetch the current one.</summary>
    Task SlideshowChangedAsync(Guid sessionId, CancellationToken cancellationToken);
}
