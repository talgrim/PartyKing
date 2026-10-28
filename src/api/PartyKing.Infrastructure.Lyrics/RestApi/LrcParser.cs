using System.Globalization;
using System.Text.RegularExpressions;
using PartyKing.Domain.Entities.Lyrics;

namespace PartyKing.Infrastructure.Lyrics.RestApi;

/// <summary>Reads the LRC format: lines such as <c>[01:23.45] some words</c>, one or more time stamps per line.</summary>
public static partial class LrcParser
{
    [GeneratedRegex(@"\[(\d{1,3}):(\d{1,2})(?:[.:](\d{1,3}))?\]")]
    private static partial Regex TimeStamp();

    /// <summary>Returns the lines ordered by time. Metadata tags such as <c>[ar:Artist]</c> are skipped.</summary>
    public static IReadOnlyList<LyricsLine> Parse(string? lrc)
    {
        if (string.IsNullOrWhiteSpace(lrc))
        {
            return [];
        }

        var lines = new List<LyricsLine>();

        foreach (var raw in lrc.Split('\n'))
        {
            var matches = TimeStamp().Matches(raw);
            if (matches.Count == 0)
            {
                continue;
            }

            var text = TimeStamp().Replace(raw, string.Empty).Trim();

            foreach (Match match in matches)
            {
                lines.Add(new LyricsLine(ToMilliseconds(match), text));
            }
        }

        return lines.OrderBy(line => line.TimeMs).ToList();
    }

    private static int ToMilliseconds(Match match)
    {
        var minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var seconds = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

        // The fraction is hundredths in practice, but be lenient: "5" is 500 ms, "05" 50 ms and "050" 50 ms
        var fraction = match.Groups[3].Success
            ? int.Parse(match.Groups[3].Value.PadRight(3, '0')[..3], CultureInfo.InvariantCulture)
            : 0;

        return (minutes * 60 + seconds) * 1000 + fraction;
    }
}
