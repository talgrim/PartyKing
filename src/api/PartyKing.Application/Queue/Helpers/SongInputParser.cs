using System.Text.RegularExpressions;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Queue.Helpers;

public abstract record ParsedSongInput(QueueItemSource Source);

public sealed record SpotifyTrackInput(string TrackId) : ParsedSongInput(QueueItemSource.Spotify);

public sealed record YoutubeInput(string Url) : ParsedSongInput(QueueItemSource.Youtube);

public static partial class SongInputParser
{
    private static readonly string[] YoutubeHosts =
        ["youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com", "youtu.be"];

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
            return new YoutubeInput(uri.ToString());
        }

        return null;
    }
}
