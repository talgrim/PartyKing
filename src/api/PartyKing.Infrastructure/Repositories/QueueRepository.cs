using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities;
using PartyKing.Domain.Enums;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

public interface IQueueRepository
{
    Task<QueueItem> AddAsync(QueueItem item, CancellationToken cancellationToken);

    /// <summary>The songs waiting to be played, in order. The song being played is not among them.</summary>
    Task<IReadOnlyList<QueueItem>> GetPendingAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<QueueItem?> GetPlayingAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the current song as played and starts the next queued one, but only when the song playing now is
    /// <paramref name="currentItemId"/> (null: nothing is playing). A caller with an outdated view, such as a
    /// player whose song an admin has already skipped, advances nothing. Returns the song playing afterwards.
    /// </summary>
    Task<QueueItem?> AdvanceAsync(Guid sessionId, int? currentItemId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a song that is still waiting in the queue. A song of another session counts as not found, and a song
    /// that is playing or already played is left alone.
    /// </summary>
    Task<QueueItemRemoval> RemoveAsync(Guid sessionId, int itemId, CancellationToken cancellationToken);
}

public enum QueueItemRemoval
{
    Removed,
    NotFound,
    NotQueued
}

internal class QueueRepository : IQueueRepository
{
    private readonly IDbContextFactory<ReaderDbContext> _readerDbContextFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerDbContextFactory;

    // A fixed set of locks, picked by session id, so memory stays bounded however many sessions come and go.
    // Two sessions sharing a lock only wait for each other briefly.
    private static readonly SemaphoreSlim[] AdvanceLocks =
        Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public QueueRepository(
        IDbContextFactory<ReaderDbContext> readerDbContextFactory,
        IDbContextFactory<WriterDbContext> writerDbContextFactory)
    {
        _readerDbContextFactory = readerDbContextFactory;
        _writerDbContextFactory = writerDbContextFactory;
    }

    public async Task<QueueItem> AddAsync(QueueItem item, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.QueueItems.AddAsync(item, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return item;
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

        await advanceLock.WaitAsync(cancellationToken);
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

        await advanceLock.WaitAsync(cancellationToken);
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

    private static SemaphoreSlim GetAdvanceLock(Guid sessionId)
    {
        return AdvanceLocks[(uint)sessionId.GetHashCode() % (uint)AdvanceLocks.Length];
    }
}
