using PartyKing.Application.Events;
using PartyKing.Domain.Events;
using PartyKing.Domain.Repositories;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Enums;
using PartyKing.Infrastructure.Metrics;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

internal class QueueRepository : IQueueRepository
{
    private readonly IDbContextFactory<ReaderDbContext> _readerDbContextFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerDbContextFactory;
    private readonly RepositoryMetrics _metrics;
    private readonly IDomainEventDispatcher _dispatcher;

    // A fixed set of locks, picked by session id, so memory stays bounded however many sessions come and go.
    // Two sessions sharing a lock only wait for each other briefly.
    private static readonly SemaphoreSlim[] AdvanceLocks =
        Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public QueueRepository(
        IDbContextFactory<ReaderDbContext> readerDbContextFactory,
        IDbContextFactory<WriterDbContext> writerDbContextFactory,
        RepositoryMetrics metrics,
        IDomainEventDispatcher dispatcher)
    {
        _readerDbContextFactory = readerDbContextFactory;
        _writerDbContextFactory = writerDbContextFactory;
        _metrics = metrics;
        _dispatcher = dispatcher;
    }

    public async Task<QueueItem> AddAsync(QueueItem item, CancellationToken cancellationToken)
    {
        // Adding has no precondition, so there is nothing to load (and no lock)
        var queue = SessionQueue.Of(item.SessionId, []);
        queue.Enqueue(item);

        await using (var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            await dbContext.QueueItems.AddAsync(item, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await DispatchAsync(queue.DrainEvents());

        return item;
    }

    public Task<QueueItem?> AddIfNoneQueuedAsync(QueueItem item, CancellationToken cancellationToken)
    {
        return WriteAsync(item.SessionId, nameof(AddIfNoneQueuedAsync), async events =>
        {
            await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

            var queue = await LoadQueueAsync(dbContext, item.SessionId, null, cancellationToken);
            var added = queue.EnqueueIfNoneQueued(item);

            if (added is null)
            {
                return null;
            }

            await dbContext.QueueItems.AddAsync(added, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            events.AddRange(queue.DrainEvents());

            return added;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<QueueItem>> GetPendingAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.QueueItems
            .AsNoTracking()
            .Where(x => x.SessionId == sessionId && x.Status == QueueItemStatus.Queued)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<QueueItem?> GetPlayingAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.QueueItems
            .AsNoTracking()
            .Where(x => x.SessionId == sessionId && x.Status == QueueItemStatus.Playing)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QueueItem>> GetAllAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.QueueItems
            .AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<QueueAdvanceResult> AdvanceAsync(
        Guid sessionId,
        int? currentItemId,
        CancellationToken cancellationToken)
    {
        return WriteAsync(sessionId, nameof(AdvanceAsync), async events =>
        {
            await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

            var queue = await LoadQueueAsync(dbContext, sessionId, null, cancellationToken);
            var result = queue.Advance(currentItemId);

            if (!result.Advanced)
            {
                return result;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            events.AddRange(queue.DrainEvents());

            return result;
        }, cancellationToken);
    }

    public Task<QueueItemRemoval> RemoveAsync(Guid sessionId, int itemId, CancellationToken cancellationToken)
    {
        return WriteAsync(sessionId, nameof(RemoveAsync), async events =>
        {
            await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

            var queue = await LoadQueueAsync(dbContext, sessionId, itemId, cancellationToken);
            var removal = queue.Remove(itemId);

            if (removal.Item is null)
            {
                return removal.Outcome;
            }

            dbContext.QueueItems.Remove(removal.Item);
            await dbContext.SaveChangesAsync(cancellationToken);

            events.AddRange(queue.DrainEvents());

            return removal.Outcome;
        }, cancellationToken);
    }

    /// <summary>
    /// Loads the songs the queue acts on, tracked by the context: the ones playing or waiting, plus
    /// <paramref name="alsoItemId"/> whatever its state, so a removal can tell a played song from an unknown one.
    /// Played songs pile up over a night and no rule looks at them.
    /// </summary>
    private static async Task<SessionQueue> LoadQueueAsync(
        WriterDbContext dbContext,
        Guid sessionId,
        int? alsoItemId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.QueueItems
            .Where(x => x.SessionId == sessionId && (x.Status != QueueItemStatus.Played || x.Id == alsoItemId))
            .ToListAsync(cancellationToken);

        return SessionQueue.Of(sessionId, items);
    }

    /// <summary>
    /// Runs a write under the session's lock. The write adds the events of what it saved to the list it is given; they
    /// are dispatched after the lock is released, so a handler may write to the same session again (the fallback
    /// refill does), and with no cancellation: the change is committed whether or not the request is still wanted.
    /// </summary>
    private async Task<TResult> WriteAsync<TResult>(
        Guid sessionId,
        string operation,
        Func<List<IDomainEvent>, Task<TResult>> write,
        CancellationToken cancellationToken)
    {
        var advanceLock = GetAdvanceLock(sessionId);
        await WaitForAdvanceLockAsync(operation, advanceLock, cancellationToken);

        var events = new List<IDomainEvent>();
        TResult result;

        try
        {
            result = await write(events);
        }
        finally
        {
            advanceLock.Release();
        }

        await DispatchAsync(events);

        return result;
    }

    private async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events)
    {
        if (events.Count > 0)
        {
            await _dispatcher.DispatchAsync(events, CancellationToken.None);
        }
    }

    private async Task WaitForAdvanceLockAsync(
        string operation,
        SemaphoreSlim advanceLock,
        CancellationToken cancellationToken)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var acquired = false;

        try
        {
            await advanceLock.WaitAsync(cancellationToken);
            acquired = true;
        }
        finally
        {
            var waitMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            _metrics.RecordAdvanceLockWait(operation, waitMs, acquired);
        }
    }

    private static SemaphoreSlim GetAdvanceLock(Guid sessionId)
    {
        return AdvanceLocks[(uint)sessionId.GetHashCode() % (uint)AdvanceLocks.Length];
    }
}
