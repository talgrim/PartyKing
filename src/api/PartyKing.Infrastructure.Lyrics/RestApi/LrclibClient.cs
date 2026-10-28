using RestEase;
using Microsoft.Extensions.Logging;
using PartyKing.Domain.Entities.Lyrics;
using PartyKing.Domain.Results;

namespace PartyKing.Infrastructure.Lyrics.RestApi;

public interface ILrclibClient
{
    /// <summary>
    /// Searches LRCLIB's free, keyless catalogue. Either both <paramref name="trackName"/> (plus optionally
    /// <paramref name="artistName"/>) or a free-text <paramref name="query"/> is used; the query wins when given.
    /// </summary>
    Task<LyricsSearchResult> SearchAsync(
        string? trackName,
        string? artistName,
        string? query,
        CancellationToken cancellationToken);
}

public sealed class LrclibClient : ILrclibClient
{
    private readonly ILrclibApi _api;
    private readonly ILogger<LrclibClient> _logger;

    public LrclibClient(ILrclibApi api, ILogger<LrclibClient> logger)
    {
        _api = api;
        _logger = logger;
    }

    public async Task<LyricsSearchResult> SearchAsync(
        string? trackName,
        string? artistName,
        string? query,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Search(trackName, artistName, query, cancellationToken);

            if (!response.ResponseMessage.IsSuccessStatusCode)
            {
                _logger.LogWarning("LRCLIB answered {StatusCode} to a search", (int)response.ResponseMessage.StatusCode);
                return LyricsSearchResult.Unavailable;
            }

            var body = response.GetContent();
            if (body is null)
            {
                return LyricsSearchResult.Unavailable;
            }

            var candidates = body
                .Where(item => item.TrackName is not null)
                .Select(item => new LyricsCandidate(
                    item.TrackName!,
                    item.ArtistName ?? string.Empty,
                    item.Duration ?? 0,
                    item.Instrumental,
                    LrcParser.Parse(item.SyncedLyrics)))
                .ToList();

            return new LyricsSearchResult(LyricsSearchStatus.Completed, candidates);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or ApiException
                                      or System.Text.Json.JsonException
                                      && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(e, "Could not search LRCLIB");
            return LyricsSearchResult.Unavailable;
        }
    }

    // The free-text query wins over the track and artist
    private Task<Response<List<LrclibSearchItem>>> Search(
        string? trackName,
        string? artistName,
        string? query,
        CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(query)
            ? _api.SearchAsync(
                trackName ?? string.Empty,
                string.IsNullOrWhiteSpace(artistName) ? null : artistName,
                null,
                cancellationToken)
            : _api.SearchAsync(null, null, query, cancellationToken);
}
