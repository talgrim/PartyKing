using PartyKing.Application.Ports;
using PartyKing.Domain.Repositories;
using ErrorOr;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Sessions;
using PartyKing.Contract.V1.Lyrics;
using PartyKing.Domain.Entities.Lyrics;
using PartyKing.Domain.Results;

namespace PartyKing.Application.Lyrics.Services;

public interface ILyricsService
{
    /// <summary>
    /// The time-stamped lyrics of the song being played, for anyone who unlocked the session. NotFound when nothing
    /// is playing or no matching lyrics exist.
    /// </summary>
    Task<ErrorOr<LyricsDto>> GetForPlayingSongAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the timing offset of the song being played for every session and pushes it to this session's viewers.
    /// Only the host (<paramref name="caller"/>) or an admin may; anyone else gets Forbidden. NotFound when
    /// nothing is playing, Validation when the offset is out of range.
    /// </summary>
    Task<ErrorOr<Success>> SetOffsetAsync(
        Guid sessionId,
        int offsetMs,
        SessionCaller caller,
        CancellationToken cancellationToken);
}

internal class LyricsService : ManagerGuardedService, ILyricsService
{
    private readonly IQueueRepository _queueRepository;
    private readonly ILrclibClient _lrclibClient;
    private readonly ILyricsCache _cache;
    private readonly ILyricsOffsetsRepository _offsetsRepository;

    public LyricsService(
        IQueueRepository queueRepository,
        ILrclibClient lrclibClient,
        ILyricsCache cache,
        ILyricsOffsetsRepository offsetsRepository,
        ISessionAuthorizer sessionAuthorizer)
        : base(sessionAuthorizer)
    {
        _queueRepository = queueRepository;
        _lrclibClient = lrclibClient;
        _cache = cache;
        _offsetsRepository = offsetsRepository;
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

        if (lines is null)
        {
            return Error.NotFound(description: "No synchronized lyrics were found for this song");
        }

        var offsetMs = await _offsetsRepository.GetAsync(playing.Url, cancellationToken);

        return new LyricsDto(
            playing.Id,
            lines.Select(line => new LyricsLineDto(line.TimeMs, line.Text)).ToList(),
            offsetMs);
    }

    public Task<ErrorOr<Success>> SetOffsetAsync(
        Guid sessionId,
        int offsetMs,
        SessionCaller caller,
        CancellationToken cancellationToken)
    {
        return RunAsManagerAsync(
            sessionId,
            caller,
            SetOffsetAsyncCore,
            cancellationToken);

        async Task<ErrorOr<Success>> SetOffsetAsyncCore()
        {
            // Refused before anything is looked up, like before; the rule is the aggregate's
            var rangeFailure = LyricsOffset.Validate(offsetMs);
            if (rangeFailure != LyricsOffsetFailure.None)
            {
                return ToError(rangeFailure);
            }

            var playing = await _queueRepository.GetPlayingAsync(sessionId, cancellationToken);
            if (playing is null)
            {
                return Error.NotFound(description: "Nothing is playing");
            }

            // The push to the session's viewers is the handler of the LyricsOffsetChanged event
            var failure = await _offsetsRepository.ChangeAsync(
                playing.Url,
                offsetMs,
                sessionId,
                playing.Id,
                cancellationToken);

            return failure == LyricsOffsetFailure.None ? Result.Success : ToError(failure);
        }
    }

    private static Error ToError(LyricsOffsetFailure failure)
    {
        return Error.Validation(description: $"The offset must be within {LyricsOffset.MaxOffsetMs / 1000} seconds");
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
