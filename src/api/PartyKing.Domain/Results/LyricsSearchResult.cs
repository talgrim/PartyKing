using PartyKing.Domain.Entities.Lyrics;

namespace PartyKing.Domain.Results;

public enum LyricsSearchStatus
{
    /// <summary>The source answered; <see cref="LyricsSearchResult.Candidates"/> may still be empty.</summary>
    Completed,

    /// <summary>The source could not be reached or answered unexpectedly, so nothing is known about the song.</summary>
    Unavailable
}

public record LyricsSearchResult(LyricsSearchStatus Status, IReadOnlyList<LyricsCandidate> Candidates)
{
    public static LyricsSearchResult Unavailable { get; } = new(LyricsSearchStatus.Unavailable, []);
}
