using System.Globalization;

namespace PartyKing.Application.Localization;

/// <summary>Reads the UI language from an <c>Accept-Language</c> header value.</summary>
public static class UiLanguageParser
{
    // The header comes from the client, so only the first entries are looked at
    private const int MaxEntries = 20;

    /// <summary>
    /// The supported language with the highest quality value (the first one on a tie, regional tags like
    /// <c>pl-PL</c> count as their primary language), or English when the header is missing or names none.
    /// </summary>
    public static UiLanguage Parse(string? acceptLanguage)
    {
        var best = UiLanguage.En;
        var bestQuality = 0d;

        foreach (var entry in (acceptLanguage ?? string.Empty).Split(',', MaxEntries))
        {
            var parts = entry.Split(';', StringSplitOptions.TrimEntries);
            if (ParseLanguage(parts[0]) is not { } language)
            {
                continue;
            }

            var quality = ParseQuality(parts.Skip(1));
            if (quality > bestQuality)
            {
                best = language;
                bestQuality = quality;
            }
        }

        return best;
    }

    private static UiLanguage? ParseLanguage(string tag)
    {
        var primary = tag.Split('-', 2)[0];

        if (primary.Equals("en", StringComparison.OrdinalIgnoreCase))
        {
            return UiLanguage.En;
        }

        return primary.Equals("pl", StringComparison.OrdinalIgnoreCase) ? UiLanguage.Pl : null;
    }

    /// <summary>The <c>q</c> parameter; 1 when absent, 0 (never chosen) when malformed.</summary>
    private static double ParseQuality(IEnumerable<string> parameters)
    {
        foreach (var parameter in parameters)
        {
            if (!parameter.StartsWith("q=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return double.TryParse(parameter[2..], NumberStyles.Float, CultureInfo.InvariantCulture, out var quality)
                ? quality
                : 0;
        }

        return 1;
    }
}
