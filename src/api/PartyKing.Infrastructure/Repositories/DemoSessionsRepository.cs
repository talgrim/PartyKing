using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Demo;
using PartyKing.Domain.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

internal class DemoSessionsRepository : IDemoSessionsRepository
{
    private readonly IDbContextFactory<ReaderDbContext> _readerDbContextFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerDbContextFactory;

    public DemoSessionsRepository(
        IDbContextFactory<ReaderDbContext> readerDbContextFactory,
        IDbContextFactory<WriterDbContext> writerDbContextFactory)
    {
        _readerDbContextFactory = readerDbContextFactory;
        _writerDbContextFactory = writerDbContextFactory;
    }

    public async Task AddAsync(DemoSession demoSession, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.DemoSessions.Add(demoSession);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DemoSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.DemoSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SessionId == sessionId, cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.DemoSessions.CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DemoSession>> ListExpiredAsync(DateTime now, CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.DemoSessions
            .AsNoTracking()
            .Where(x => x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt)
            .ToListAsync(cancellationToken);
    }
}
