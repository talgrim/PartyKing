using System.Text.Json;

namespace PartyKing.Application.Games.Services;

internal static class SuggestionParser
{
    /// <summary>
    /// Reads a JSON array of strings, tolerating a markdown fence around it. The text is untrusted model output, so
    /// anything that is not a short plain string is dropped, and the count is capped.
    /// </summary>
    internal static IReadOnlyList<string> Parse(string? text, int count, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            return [];
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<string?>>(text[start..(end + 1)]) ?? [];

            return items
                .Select(item => item?.Trim())
                .Where(item => !string.IsNullOrEmpty(item) && item.Length <= maxLength)
                .Select(item => item!)
                .Distinct()
                .Take(count)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
