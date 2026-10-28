using System.Text.RegularExpressions;

namespace PartyKing.Application.Queue.Helpers;

public static partial class PlaylistInputParser
{
    [GeneratedRegex(@"^spotify:playlist:(?<id>[A-Za-z0-9]{22})$")]
    private static partial Regex SpotifyUriRegex();

    [GeneratedRegex(@"^/(intl-[a-z]{2,}(-[A-Za-z]+)?/)?playlist/(?<id>[A-Za-z0-9]{22})/?$")]
    private static partial Regex SpotifyPathRegex();

    /// <summary>The Spotify playlist id of a spotify:playlist: URI or an open.spotify.com playlist link; null otherwise.</summary>
    public static string? Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        input = input.Trim();

        var uriMatch = SpotifyUriRegex().Match(input);
        if (uriMatch.Success)
        {
            return uriMatch.Groups["id"].Value;
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || uri.Host != "open.spotify.com")
        {
            return null;
        }

        var pathMatch = SpotifyPathRegex().Match(uri.AbsolutePath);

        return pathMatch.Success ? pathMatch.Groups["id"].Value : null;
    }
}
