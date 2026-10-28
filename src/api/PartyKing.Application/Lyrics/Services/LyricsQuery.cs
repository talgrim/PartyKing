using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Enums;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PartyKing.Application.Lyrics.Services;

/// <param name="Title">The song's title with the noise stripped.</param>
/// <param name="Artist">The artist, when known with some confidence.</param>
/// <param name="FullText">Title and artist together, for a free-text search and for matching.</param>
/// <param name="DurationMs">The length of the song when the queue knows it; YouTube items don't.</param>
internal partial record LyricsQuery(string Title, string? Artist, string FullText, int? DurationMs)
{
    public static LyricsQuery From(QueueItem item) =>
        item.Source == QueueItemSource.Spotify ? FromSpotify(item) : FromYoutube(item);

    // Spotify gives clean fields, apart from suffixes like "- Remastered 2011"
    private static LyricsQuery FromSpotify(QueueItem item)
    {
        var title = CleanTitle(item.Title);
        var artist = item.Artists?.Split(',')[0].Trim();

        return new LyricsQuery(title, string.IsNullOrEmpty(artist) ? null : artist, $"{artist} {title}".Trim(), item.DurationMs);
    }

    // A video title is free text: "Artist - Song (Official Video)" is the common shape, the channel is often a label
    private static LyricsQuery FromYoutube(QueueItem item)
    {
        var cleaned = CleanTitle(item.Title);
        var parts = Separator().Split(cleaned, 2);

        return parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0
            ? new LyricsQuery(parts[1].Trim(), parts[0].Trim(), cleaned, item.DurationMs)
            : new LyricsQuery(cleaned, null, cleaned, item.DurationMs);
    }

    private static string CleanTitle(string title)
    {
        var cleaned = Brackets().Replace(title, " ");
        cleaned = RemasterSuffix().Replace(cleaned, string.Empty);
        cleaned = VideoNoise().Replace(cleaned, " ");

        return Whitespace().Replace(cleaned, " ").Trim(' ', '-', '–', '—', '|');
    }

    /// <summary>Lower case letters and digits only, without accents: what two spellings of a title have in common.</summary>
    public static string Normalize(string text)
    {
        var builder = new StringBuilder();

        foreach (var c in text.Replace('ł', 'l').Replace('Ł', 'L').Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"\([^)]*\)|\[[^\]]*\]|【[^】]*】")]
    private static partial Regex Brackets();

    [GeneratedRegex(@"\s+[-–]\s+(remaster(ed)?|live|mono|stereo|single|radio edit|album version|version)\b.*$", RegexOptions.IgnoreCase)]
    private static partial Regex RemasterSuffix();

    [GeneratedRegex(@"\b(official\s+(music\s+)?(video|audio)|lyric\s+video|lyrics?|video\s+clip|hd|hq|4k)\b", RegexOptions.IgnoreCase)]
    private static partial Regex VideoNoise();

    [GeneratedRegex(@"\s+[-–—|]\s+")]
    private static partial Regex Separator();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
