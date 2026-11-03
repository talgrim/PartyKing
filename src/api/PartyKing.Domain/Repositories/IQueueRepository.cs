using PartyKing.Domain.Entities.Queue;

namespace PartyKing.Domain.Repositories;

public interface IQueueRepository
{
    Task<QueueItem> AddAsync(QueueItem item, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the item only when no song of the session is waiting, under the same lock as advancing and removing.
    /// Returns null (and adds nothing) when one is, e.g. because a guest queued a song in the meantime.
    /// </summary>
    /// <remarks>
    /// Saved changes raise <c>SongQueued</c>, <c>SongRemoved</c> and <c>QueueAdvanced</c>, dispatched once the change is
    /// committed and the session's lock is released.
    /// </remarks>
    Task<QueueItem?> AddIfNoneQueuedAsync(QueueItem item, CancellationToken cancellationToken);

    /// <summary>The songs waiting to be played, in order. The song being played is not among them.</summary>
    Task<IReadOnlyList<QueueItem>> GetPendingAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<QueueItem?> GetPlayingAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the current song as played and starts the next queued one, but only when the song playing now is
    /// <paramref name="currentItemId"/> (null: nothing is playing). A caller with an outdated view, such as a
    /// player whose song an admin has already skipped, advances nothing. The result holds the song playing afterwards.
    /// </summary>
    Task<QueueAdvanceResult> AdvanceAsync(Guid sessionId, int? currentItemId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a song that is still waiting in the queue. A song of another session counts as not found, and a song
    /// that is playing or already played is left alone.
    /// </summary>
    Task<QueueItemRemoval> RemoveAsync(Guid sessionId, int itemId, CancellationToken cancellationToken);
}
