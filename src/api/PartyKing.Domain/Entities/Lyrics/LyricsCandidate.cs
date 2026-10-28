namespace PartyKing.Domain.Entities.Lyrics;

/// <param name="SyncedLines">The time-stamped lines, ordered by time; empty when the source has only plain text.</param>
public record LyricsCandidate(
    string TrackName,
    string ArtistName,
    double DurationSeconds,
    bool Instrumental,
    IReadOnlyList<LyricsLine> SyncedLines);
