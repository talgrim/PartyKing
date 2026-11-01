using PartyKing.Application.Events;
using PartyKing.Domain.Events;
using PartyKing.Domain.Events.Queue;
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
        await using (var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            await dbContext.QueueItems.AddAsync(item, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await DispatchAsync([new SongQueued(item.SessionId, item)]);

        return item;
    }

    public Task<QueueItem?> AddIfNoneQueuedAsync(QueueItem item, CancellationToken cancellationToken)
    {
        return WriteAsync(item.SessionId, nameof(AddIfNoneQueuedAsync), async events =>
        {
            await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

            var anyQueued = await dbContext.QueueItems
                .AnyAsync(x => x.SessionId == item.SessionId && x.Status == QueueItemStatus.Queued, cancellationToken);

            if (anyQueued)
            {
                return null;
            }

            await dbContext.QueueItems.AddAsync(item, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            events.Add(new SongQueued(item.SessionId, item));

            return item;
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

    public Task<QueueAdvanceResult> AdvanceAsync(
        Guid sessionId,
        int? currentItemId,
        CancellationToken cancellationToken)
    {
        return WriteAsync(sessionId, nameof(AdvanceAsync), async events =>
        {
            await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

            var playing = await dbContext.QueueItems
                .Where(x => x.SessionId == sessionId && x.Status == QueueItemStatus.Playing)
                .ToListAsync(cancellationToken);

            if (playing.FirstOrDefault()?.Id != currentItemId)
            {
                return new QueueAdvanceResult(playing.FirstOrDefault(), Advanced: false, PreviousSongId: null);
            }

            foreach (var item in playing)
            {
                item.MarkPlayed();
            }

            var next = await dbContext.QueueItems
                .Where(x => x.SessionId == sessionId && x.Status == QueueItemStatus.Queued)
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            next?.StartPlaying();

            await dbContext.SaveChangesAsync(cancellationToken);

            events.Add(new QueueAdvanced(sessionId, currentItemId, next));

            return new QueueAdvanceResult(next, Advanced: true, PreviousSongId: currentItemId);
        }, cancellationToken);
    }

    public Task<QueueItemRemoval> RemoveAsync(Guid sessionId, int itemId, CancellationToken cancellationToken)
    {
        return WriteAsync(sessionId, nameof(RemoveAsync), async events =>
        {
            await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

            var item = await dbContext.QueueItems
                .FirstOrDefaultAsync(x => x.Id == itemId && x.SessionId == sessionId, cancellationToken);

            if (item is null)
            {
                return QueueItemRemoval.NotFound;
            }

            if (item.Status != QueueItemStatus.Queued)
            {
                return QueueItemRemoval.NotQueued;
            }

            dbContext.QueueItems.Remove(item);
            await dbContext.SaveChangesAsync(cancellationToken);

            events.Add(new SongRemoved(sessionId, itemId));

            return QueueItemRemoval.Removed;
        }, cancellationToken);
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
