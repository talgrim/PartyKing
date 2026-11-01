using ErrorOr;
using Microsoft.Extensions.Logging;
using PartyKing.Application.Mappings;
using PartyKing.Contract.V1.Queue;
using PartyKing.Domain.Repositories;

namespace PartyKing.Application.Queue.Services;

/// <summary>
/// Moves a session's queue to its next song: the one step the host's end-of-song call and an admin's skip share.
/// Pushing the change and queueing the playlist song that follows are handlers of the events the repository raises.
/// </summary>
public interface IQueueAdvancer
{
    /// <summary>
    /// Advances only when <paramref name="currentSongId"/> matches the song playing now (same rule as the repository);
    /// otherwise nothing changes and the song that is really playing is returned. Not found when no song plays afterwards.
    /// </summary>
    Task<ErrorOr<QueueItemDto>> AdvanceAsync(Guid sessionId, int? currentSongId, CancellationToken cancellationToken);
}

internal sealed class QueueAdvancer : IQueueAdvancer
{
    private readonly IQueueRepository _queueRepository;
    private readonly IFallbackQueueFiller _fallbackFiller;
    private readonly ILogger<QueueAdvancer> _logger;

    public QueueAdvancer(
        IQueueRepository queueRepository,
        IFallbackQueueFiller fallbackFiller,
        ILogger<QueueAdvancer> logger)
    {
        _queueRepository = queueRepository;
        _fallbackFiller = fallbackFiller;
        _logger = logger;
    }

    public async Task<ErrorOr<QueueItemDto>> AdvanceAsync(
        Guid sessionId,
        int? currentSongId,
        CancellationToken cancellationToken)
    {
        // An empty queue gets a playlist song first (a previous refill may have failed), so the advance finds one.
        // This cannot be a handler: it has to happen before the advance, not after it.
        await _fallbackFiller.EnsureQueuedAsync(sessionId, cancellationToken);

        var result = await _queueRepository.AdvanceAsync(sessionId, currentSongId, cancellationToken);

        if (result.Advanced)
        {
            _logger.LogInformation(
                "Session {SessionId} advanced from song {PreviousSongId} to {NextSongId}",
                sessionId,
                result.PreviousSongId,
                result.Playing?.Id);
        }

        return result.Playing is null
            ? Error.NotFound(description: "The queue is empty")
            : result.Playing.ToDto();
    }
}
