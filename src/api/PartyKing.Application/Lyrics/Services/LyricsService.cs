using ErrorOr;
using PartyKing.Contract.V1.Lyrics;
using PartyKing.Domain.Entities.Lyrics;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Lyrics.RestApi;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Lyrics.Services;

public interface ILyricsService
{
    /// <summary>
    /// The time-stamped lyrics of the song being played, for anyone who unlocked the session. NotFound when nothing
    /// is playing or no matching lyrics exist.
    /// </summary>
    Task<ErrorOr<LyricsDto>> GetForPlayingSongAsync(Guid sessionId, CancellationToken cancellationToken);
}

internal class LyricsService : ILyricsService
{
    private readonly IQueueRepository _queueRepository;
    private readonly ILrclibClient _lrclibClient;
    private readonly ILyricsCache _cache;

    public LyricsService(
        IQueueRepository queueRepository,
        ILrclibClient lrclibClient,
        ILyricsCache cache)
    {
        _queueRepository = queueRepository;
        _lrclibClient = lrclibClient;
        _cache = cache;
    }

    public async Task<ErrorOr<LyricsDto>> GetForPlayingSongAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var playing = await _queueRepository.GetPlayingAsync(sessionId, cancellationToken);
        if (playing is null)
        {
            return Error.NotFound(description: "Nothing is playing");
        }

        if (!_cache.TryGet(playing.Url, out var lines))
        {
            var (found, searched) = await FindAsync(LyricsQuery.From(playing), cancellationToken);
            lines = found;

            // An unreachable LRCLIB says nothing about the song, so don't remember it as lyric-less
            if (searched)
            {
                _cache.Set(playing.Url, lines);
            }
        }

        return lines is null
            ? Error.NotFound(description: "No synchronized lyrics were found for this song")
            : new LyricsDto(playing.Id, lines.Select(line => new LyricsLineDto(line.TimeMs, line.Text)).ToList());
    }

    private async Task<(IReadOnlyList<LyricsLine>? Lines, bool Searched)> FindAsync(
        LyricsQuery query,
        CancellationToken cancellationToken)
    {
        // The precise search first, then a free-text one for titles that are not split into track and artist
        var attempts = new (string? Track, string? Artist, string? Text)[]
        {
            (query.Title, query.Artist, null),
            (null, null, query.FullText)
        };

        var searched = true;
        foreach (var (track, artist, text) in attempts)
        {
            var result = await _lrclibClient.SearchAsync(track, artist, text, cancellationToken);
            if (result.Status == LyricsSearchStatus.Unavailable)
            {
                searched = false;
                continue;
            }

            if (LyricsMatcher.Pick(query, result.Candidates) is { } lines)
            {
                return (lines, true);
            }
        }

        return (null, searched);
    }
}
