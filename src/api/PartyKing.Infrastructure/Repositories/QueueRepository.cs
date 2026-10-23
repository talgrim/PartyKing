using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities;
using PartyKing.Domain.Enums;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

public interface IQueueRepository
{
    Task<QueueItem> AddAsync(QueueItem item, CancellationToken cancellationToken);
    Task<IReadOnlyList<QueueItem>> GetPendingAsync(CancellationToken cancellationToken);
    Task<QueueItem?> AdvanceAsync(CancellationToken cancellationToken);
}

internal class QueueRepository : IQueueRepository
{
    private readonly IDbContextFactory<ReaderDbContext> _readerDbContextFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerDbContextFactory;
    private readonly SemaphoreSlim _advanceLock = new(1, 1);

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

    public async Task<IReadOnlyList<QueueItem>> GetPendingAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.QueueItems
            .AsNoTracking()
            .Where(x => x.Status != QueueItemStatus.Played)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Marks the current item as played and starts the next queued one, if any.</summary>
    public async Task<QueueItem?> AdvanceAsync(CancellationToken cancellationToken)
    {
        await _advanceLock.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

            var playing = await dbContext.QueueItems
                .Where(x => x.Status == QueueItemStatus.Playing)
                .ToListAsync(cancellationToken);

            foreach (var item in playing)
            {
                item.Status = QueueItemStatus.Played;
            }

            var next = await dbContext.QueueItems
                .Where(x => x.Status == QueueItemStatus.Queued)
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
            _advanceLock.Release();
        }
    }
}
