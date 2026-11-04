using PartyKing.Domain.Entities.Base;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Events.Queue;

namespace PartyKing.Domain.Entities.Queue;

/// <summary>What <see cref="SessionQueue.Remove"/> did: the outcome and, for a removal, the song to delete.</summary>
public sealed record QueueRemoval(QueueItemRemoval Outcome, QueueItem? Item);

/// <summary>
/// The queue of one session: the rules of adding, advancing and removing songs. It is not a table of its own: the
/// repository builds it from the session's <see cref="QueueItem"/> rows (the ones still alive, plus the one a removal
/// asks for), runs one action and saves what changed. The actions raise their events at once; the repository takes them
/// with <see cref="AggregateRoot.DrainEvents"/> after the save, so a <see cref="SongQueued"/> already holds the database
/// id by the time a handler sees it.
/// </summary>
public sealed class SessionQueue : AggregateRoot
{
    private readonly List<QueueItem> _items;

    private SessionQueue(Guid sessionId, List<QueueItem> items)
    {
        SessionId = sessionId;
        _items = items;
    }

    public Guid SessionId { get; }

    /// <summary>
    /// Builds the queue from the session's rows. Only the songs the action needs have to be given: a queue with none
    /// can still take a new song.
    /// </summary>
    public static SessionQueue Of(Guid sessionId, IEnumerable<QueueItem> items)
    {
        var own = items.OrderBy(x => x.Id).ToList();

        if (own.Any(x => x.SessionId != sessionId))
        {
            throw new ArgumentException("The song belongs to another session.", nameof(items));
        }

        return new SessionQueue(sessionId, own);
    }

    /// <summary>Adds a song at the end of the queue. The caller saves it; its id comes from the database.</summary>
    public QueueItem Enqueue(QueueItem item)
    {
        EnsureOwn(item);

        _items.Add(item);
        Raise(new SongQueued(SessionId, item));

        return item;
    }

    /// <summary>
    /// Adds the song only when no song of the session is waiting, e.g. a fallback song that must not queue up behind a
    /// guest's. Returns null (and raises nothing) when one is.
    /// </summary>
    public QueueItem? EnqueueIfNoneQueued(QueueItem item)
    {
        if (_items.Any(x => x.Status == QueueItemStatus.Queued))
        {
            return null;
        }

        return Enqueue(item);
    }

    /// <summary>
    /// Marks the song playing now as played and starts the next waiting one, but only when the song playing now is
    /// <paramref name="currentItemId"/> (null: nothing is playing). A caller with an outdated view advances nothing and
    /// raises nothing; the result then holds the song that is really playing.
    /// </summary>
    public QueueAdvanceResult Advance(int? currentItemId)
    {
        var playing = _items.Where(x => x.Status == QueueItemStatus.Playing).ToList();
        var current = playing.FirstOrDefault();

        if (current?.Id != currentItemId)
        {
            return new QueueAdvanceResult(current, Advanced: false, PreviousSongId: null);
        }

        foreach (var item in playing)
        {
            item.MarkPlayed();
        }

        var next = _items.FirstOrDefault(x => x.Status == QueueItemStatus.Queued);
        next?.StartPlaying();

        Raise(new QueueAdvanced(SessionId, currentItemId, next));

        return new QueueAdvanceResult(next, Advanced: true, PreviousSongId: currentItemId);
    }

    /// <summary>
    /// Takes out a song that is still waiting. A song that is playing or already played stays
    /// (<see cref="QueueItemRemoval.NotQueued"/>), an unknown one is <see cref="QueueItemRemoval.NotFound"/>. The caller
    /// deletes the returned song.
    /// </summary>
    public QueueRemoval Remove(int itemId)
    {
        var item = _items.FirstOrDefault(x => x.Id == itemId);

        if (item is null)
        {
            return new QueueRemoval(QueueItemRemoval.NotFound, null);
        }

        if (item.Status != QueueItemStatus.Queued)
        {
            return new QueueRemoval(QueueItemRemoval.NotQueued, null);
        }

        _items.Remove(item);
        Raise(new SongRemoved(SessionId, itemId));

        return new QueueRemoval(QueueItemRemoval.Removed, item);
    }

    private void EnsureOwn(QueueItem item)
    {
        if (item.SessionId != SessionId)
        {
            throw new ArgumentException("The song belongs to another session.", nameof(item));
        }
    }
}
