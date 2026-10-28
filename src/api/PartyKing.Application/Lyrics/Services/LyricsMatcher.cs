using PartyKing.Domain.Entities.Lyrics;

namespace PartyKing.Application.Lyrics.Services;

/// <summary>Picks the search result that really is the song, or none: wrong lyrics are worse than no lyrics.</summary>
internal static class LyricsMatcher
{
    /// <summary>Lyrics of another cut of the song would drift, so the lengths have to agree this closely.</summary>
    public const double MaxDurationDifferenceSeconds = 5;

    public static IReadOnlyList<LyricsLine>? Pick(LyricsQuery query, IReadOnlyList<LyricsCandidate> candidates)
    {
        var title = LyricsQuery.Normalize(query.Title);
        var fullText = LyricsQuery.Normalize(query.FullText);

        var matches = candidates
            .Where(candidate => !candidate.Instrumental && candidate.SyncedLines.Count > 0)
            .Where(candidate => TitleMatches(LyricsQuery.Normalize(candidate.TrackName), title, fullText))
            .Select(candidate => (Candidate: candidate, Difference: DurationDifference(query, candidate)))
            .Where(match => match.Difference is null or <= MaxDurationDifferenceSeconds)
            .ToList();

        // Results come in order of relevance, so the stable sort keeps that among equally long ones
        return matches
            .OrderBy(match => match.Difference ?? 0)
            .Select(match => match.Candidate.SyncedLines)
            .FirstOrDefault();
    }

    // Either the track name is somewhere in the text we have ("Song" in "Artist Song"), or the other way round
    // ("Song - Remastered" for "Song"). Very short titles only match exactly, so "I" does not match everything.
    private static bool TitleMatches(string track, string title, string fullText)
    {
        if (track.Length == 0 || title.Length == 0)
        {
            return false;
        }

        if (track == title)
        {
            return true;
        }

        return track.Length >= 3 && title.Length >= 3 && (fullText.Contains(track) || track.Contains(title));
    }

    private static double? DurationDifference(LyricsQuery query, LyricsCandidate candidate)
    {
        return query.DurationMs is { } durationMs && candidate.DurationSeconds > 0
            ? Math.Abs(durationMs / 1000.0 - candidate.DurationSeconds)
            : null;
    }
}
