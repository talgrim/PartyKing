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

    // A fixed set of locks, picked by session id, so memory stays bounded however many sessions come and go.
    // Two sessions sharing a lock only wait for each other briefly.
    private static readonly SemaphoreSlim[] AdvanceLocks =
        Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public QueueRepository(
        IDbContextFactory<ReaderDbContext> readerDbContextFactory,
        IDbContextFactory<WriterDbContext> writerDbContextFactory,
        RepositoryMetrics metrics)
    {
        _readerDbContextFactory = readerDbContextFactory;
        _writerDbContextFactory = writerDbContextFactory;
        _metrics = metrics;
    }

    public async Task<QueueItem> AddAsync(QueueItem item, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.QueueItems.AddAsync(item, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return item;
    }

    public async Task<QueueItem?> AddIfNoneQueuedAsync(QueueItem item, CancellationToken cancellationToken)
    {
        var advanceLock = GetAdvanceLock(item.SessionId);

        await WaitForAdvanceLockAsync(nameof(AddIfNoneQueuedAsync), advanceLock, cancellationToken);
        try
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

            return item;
        }
        finally
        {
            advanceLock.Release();
        }
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

    public async Task<QueueItem?> AdvanceAsync(
        Guid sessionId,
        int? currentItemId,
        CancellationToken cancellationToken)
    {
        var advanceLock = GetAdvanceLock(sessionId);

        await WaitForAdvanceLockAsync(nameof(AdvanceAsync), advanceLock, cancellationToken);
        try
        {
            await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

            var playing = await dbContext.QueueItems
                .Where(x => x.SessionId == sessionId && x.Status == QueueItemStatus.Playing)
                .ToListAsync(cancellationToken);

            if (playing.FirstOrDefault()?.Id != currentItemId)
            {
                return playing.FirstOrDefault();
            }

            foreach (var item in playing)
            {
                item.Status = QueueItemStatus.Played;
            }

            var next = await dbContext.QueueItems
                .Where(x => x.SessionId == sessionId && x.Status == QueueItemStatus.Queued)
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (next is not null)
            {
                next.Status = QueueItemStatus.Playing;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return next;
        }
        finally
        {
            advanceLock.Release();
        }
    }

    public async Task<QueueItemRemoval> RemoveAsync(Guid sessionId, int itemId, CancellationToken cancellationToken)
    {
        var advanceLock = GetAdvanceLock(sessionId);

        await WaitForAdvanceLockAsync(nameof(RemoveAsync), advanceLock, cancellationToken);
        try
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

            return QueueItemRemoval.Removed;
        }
        finally
        {
            advanceLock.Release();
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
