using PartyKing.Domain.Entities.Base;
using PartyKing.Domain.Events.Lyrics;

namespace PartyKing.Domain.Entities.Lyrics;

/// <summary>Why an offset was refused.</summary>
public enum LyricsOffsetFailure
{
    None,
    OutOfRange
}

/// <summary>The timing offset the host or an admin saved for a song, shared by every session that plays it.</summary>
public class LyricsOffset : AggregateRoot
{
    /// <summary>The offset is kept within this many milliseconds either way (the UI's <c>MAX_OFFSET_MS</c> matches).</summary>
    public const int MaxOffsetMs = 100_000;

    private LyricsOffset()
    {
    }

    /// <summary>A song with no saved offset yet (0, like a song that was never saved).</summary>
    public LyricsOffset(string songUrl)
    {
        SongUrl = songUrl;
    }

    /// <summary>The song's URL (<c>QueueItem.Url</c>), the same key the lyrics cache uses.</summary>
    public string SongUrl { get; internal set; } = string.Empty;

    /// <summary>Positive shows the lyrics earlier, negative later.</summary>
    public int OffsetMs { get; internal set; }

    /// <summary>The rule of <see cref="Change"/>, for a caller that wants to refuse before it looks anything else up.</summary>
    public static LyricsOffsetFailure Validate(int offsetMs)
    {
        return Math.Abs((long)offsetMs) > MaxOffsetMs ? LyricsOffsetFailure.OutOfRange : LyricsOffsetFailure.None;
    }

    /// <summary>
    /// Saves the offset, whatever it was (saving the same value or 0 is a change like any other). The offset is global, but
    /// the viewers told about it are those of <paramref name="sessionId"/>, which played song <paramref name="songId"/>.
    /// </summary>
    public LyricsOffsetFailure Change(int offsetMs, Guid sessionId, int songId)
    {
        var failure = Validate(offsetMs);
        if (failure != LyricsOffsetFailure.None)
        {
            return failure;
        }

        OffsetMs = offsetMs;
        Raise(new LyricsOffsetChanged(sessionId, songId, SongUrl, offsetMs));

        return LyricsOffsetFailure.None;
    }
}
