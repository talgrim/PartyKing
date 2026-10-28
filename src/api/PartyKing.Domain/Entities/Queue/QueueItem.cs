using PartyKing.Domain.Enums;

namespace PartyKing.Domain.Entities.Queue;

public class QueueItem
{
    private QueueItem()
    {
    }

    public QueueItem(
        Guid sessionId,
        QueueItemSource source,
        string url,
        string title,
        string? artists,
        string? albumArtUrl,
        int? durationMs)
    {
        SessionId = sessionId;
        Source = source;
        Url = url;
        Title = title;
        Artists = artists;
        AlbumArtUrl = albumArtUrl;
        DurationMs = durationMs;
        Status = QueueItemStatus.Queued;
    }

    /// <summary>Auto-incremented; also defines the queue order.</summary>
    public int Id { get; set; }
    public Guid SessionId { get; set; }
    public QueueItemSource Source { get; set; }

    /// <summary>YouTube URL or Spotify track URI (spotify:track:...).</summary>
    public string Url { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Artists { get; set; }
    public string? AlbumArtUrl { get; set; }
    public int? DurationMs { get; set; }
    public QueueItemStatus Status { get; set; }

    /// <summary>True when the server queued the song from the session's fallback playlist.</summary>
    public bool IsFallback { get; set; }
}
