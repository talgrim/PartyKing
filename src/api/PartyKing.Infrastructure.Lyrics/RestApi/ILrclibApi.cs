using System.Text.Json.Serialization;
using RestEase;

namespace PartyKing.Infrastructure.Lyrics.RestApi;

/// <summary>The part of the LRCLIB HTTP API we use. Null parameters are left out of the query string.</summary>
public interface ILrclibApi
{
    [Get("api/search")]
    Task<Response<List<LrclibSearchItem>>> SearchAsync(
        [Query("track_name")] string? trackName,
        [Query("artist_name")] string? artistName,
        [Query("q")] string? query,
        CancellationToken cancellationToken);
}

public sealed record LrclibSearchItem(
    [property: JsonPropertyName("trackName")] string? TrackName,
    [property: JsonPropertyName("artistName")] string? ArtistName,
    [property: JsonPropertyName("duration")] double? Duration,
    [property: JsonPropertyName("instrumental")] bool Instrumental,
    [property: JsonPropertyName("syncedLyrics")] string? SyncedLyrics);
