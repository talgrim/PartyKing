using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Enums;

namespace PartyKing.Domain.Entities.Playlists;

/// <summary>
/// A song of a saved playlist. It is a copy of the queue item, not a reference: the queue goes away with its session.
/// </summary>
public class SavedPlaylistItem
{
    private SavedPlaylistItem()
    {
    }

    internal SavedPlaylistItem(int position, QueueItem song)
    {
        Position = position;
        Source = song.Source;
        Url = song.Url;
        Title = song.Title;
        Artists = song.Artists;
        AlbumArtUrl = song.AlbumArtUrl;
        DurationMs = song.DurationMs;
    }

    public int Id { get; internal set; }
    public Guid PlaylistId { get; internal set; }

    /// <summary>Zero based; the order the songs were queued in.</summary>
    public int Position { get; internal set; }
    public QueueItemSource Source { get; internal set; }

    /// <summary>YouTube URL or Spotify track URI (spotify:track:...).</summary>
    public string Url { get; internal set; } = null!;
    public string Title { get; internal set; } = null!;
    public string? Artists { get; internal set; }
    public string? AlbumArtUrl { get; internal set; }
    public int? DurationMs { get; internal set; }
}
