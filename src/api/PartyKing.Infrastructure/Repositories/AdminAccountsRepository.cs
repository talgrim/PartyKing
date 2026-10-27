using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

public interface IAdminAccountsRepository
{
    Task<bool> AnyAsync(CancellationToken cancellationToken);
    Task<AdminAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<AdminAccount?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(AdminAccount account, CancellationToken cancellationToken);
}

internal class AdminAccountsRepository : IAdminAccountsRepository
{
    private readonly IDbContextFactory<ReaderDbContext> _readerDbContextFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerDbContextFactory;

    public AdminAccountsRepository(
        IDbContextFactory<ReaderDbContext> readerDbContextFactory,
        IDbContextFactory<WriterDbContext> writerDbContextFactory)
    {
        _readerDbContextFactory = readerDbContextFactory;
        _writerDbContextFactory = writerDbContextFactory;
    }

    public async Task<bool> AnyAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.AdminAccounts.AnyAsync(cancellationToken);
    }

    public async Task<AdminAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.AdminAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Username == username, cancellationToken);
    }

    public async Task<AdminAccount?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.AdminAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(AdminAccount account, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.AdminAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
