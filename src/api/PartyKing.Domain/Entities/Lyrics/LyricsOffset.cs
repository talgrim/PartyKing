namespace PartyKing.Domain.Entities.Lyrics;

/// <summary>The timing offset the host or an admin saved for a song, shared by every session that plays it.</summary>
public class LyricsOffset
{
    /// <summary>The song's URL (<c>QueueItem.Url</c>), the same key the lyrics cache uses.</summary>
    public string SongUrl { get; set; } = string.Empty;

    /// <summary>Positive shows the lyrics earlier, negative later.</summary>
    public int OffsetMs { get; set; }
}
