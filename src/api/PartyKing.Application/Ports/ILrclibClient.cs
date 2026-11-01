using PartyKing.Domain.Results;

namespace PartyKing.Application.Ports;

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
