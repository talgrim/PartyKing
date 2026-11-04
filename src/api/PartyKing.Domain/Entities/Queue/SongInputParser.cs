using System.Text.RegularExpressions;
using PartyKing.Domain.Enums;

namespace PartyKing.Domain.Entities.Queue;

public abstract record ParsedSongInput(QueueItemSource Source);

public sealed record SpotifyTrackInput(string TrackId) : ParsedSongInput(QueueItemSource.Spotify);

public sealed record YoutubeInput(string VideoId) : ParsedSongInput(QueueItemSource.Youtube)
{
    /// <summary>The canonical watch URL, however the video was linked.</summary>
    public string Url => $"https://www.youtube.com/watch?v={VideoId}";
}

public static partial class SongInputParser
{
    private static readonly string[] YoutubeHosts =
        ["youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com", "youtu.be"];

    [GeneratedRegex(@"^[A-Za-z0-9_-]{11}$")]
    private static partial Regex YoutubeVideoIdRegex();

    [GeneratedRegex(@"^/(?:embed|shorts|live|v)/(?<id>[A-Za-z0-9_-]{11})/?$")]
    private static partial Regex YoutubePathRegex();

    [GeneratedRegex(@"^spotify:track:(?<id>[A-Za-z0-9]{22})$")]
    private static partial Regex SpotifyUriRegex();

    [GeneratedRegex(@"^/(intl-[a-z]{2,}(-[A-Za-z]+)?/)?track/(?<id>[A-Za-z0-9]{22})/?$")]
    private static partial Regex SpotifyPathRegex();

    public static ParsedSongInput? Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        input = input.Trim();

        var uriMatch = SpotifyUriRegex().Match(input);
        if (uriMatch.Success)
        {
            return new SpotifyTrackInput(uriMatch.Groups["id"].Value);
        }

        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        if (uri.Host == "open.spotify.com")
        {
            var pathMatch = SpotifyPathRegex().Match(uri.AbsolutePath);
            return pathMatch.Success ? new SpotifyTrackInput(pathMatch.Groups["id"].Value) : null;
        }

        if (YoutubeHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            var videoId = ExtractYoutubeVideoId(uri);
            return videoId is null ? null : new YoutubeInput(videoId);
        }

        return null;
    }

    /// <summary>The id of a single video; null for playlists, channels, searches and the like.</summary>
    private static string? ExtractYoutubeVideoId(Uri uri)
    {
        if (uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            var id = uri.AbsolutePath.Trim('/');
            return YoutubeVideoIdRegex().IsMatch(id) ? id : null;
        }

        if (uri.AbsolutePath == "/watch")
        {
            var id = QueryValue(uri.Query, "v");
            return id is not null && YoutubeVideoIdRegex().IsMatch(id) ? id : null;
        }

        var pathMatch = YoutubePathRegex().Match(uri.AbsolutePath);
        return pathMatch.Success ? pathMatch.Groups["id"].Value : null;
    }

    /// <summary>The value of a query parameter; null when it is missing or given more than once.</summary>
    private static string? QueryValue(string query, string name)
    {
        string? found = null;

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = Decode(separator < 0 ? pair : pair[..separator]);
            if (key != name)
            {
                continue;
            }

            if (found is not null)
            {
                return null;
            }

            found = separator < 0 ? string.Empty : Decode(pair[(separator + 1)..]);
        }

        return found;
    }

    private static string Decode(string text)
    {
        return Uri.UnescapeDataString(text.Replace('+', ' '));
    }
}
