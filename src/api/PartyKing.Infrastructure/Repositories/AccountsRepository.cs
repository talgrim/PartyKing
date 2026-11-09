using Microsoft.EntityFrameworkCore;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure.Repositories;

internal class AccountsRepository : IAccountsRepository
{
    private const char LikeEscape = '\\';

    private readonly IDbContextFactory<ReaderDbContext> _readerDbContextFactory;
    private readonly IDbContextFactory<WriterDbContext> _writerDbContextFactory;

    public AccountsRepository(
        IDbContextFactory<ReaderDbContext> readerDbContextFactory,
        IDbContextFactory<WriterDbContext> writerDbContextFactory)
    {
        _readerDbContextFactory = readerDbContextFactory;
        _writerDbContextFactory = writerDbContextFactory;
    }

    public async Task<bool> AnyAdminAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Accounts.AnyAsync(x => x.Role == AccountRole.Admin, cancellationToken);
    }

    public async Task<Account?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        // The column is NOCASE, so the comparison ignores the case
        return await dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Username == username, cancellationToken);
    }

    public async Task<Account?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<bool> TryAddAsync(Account account, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.Accounts.Add(account);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return false;
        }
    }

    public async Task<bool> ChangePasswordAsync(
        Guid id,
        string passwordHash,
        Guid newSecurityStamp,
        CancellationToken cancellationToken)
    {
        return await UpdateAsync(id, account => account.ChangePassword(passwordHash, newSecurityStamp), cancellationToken);
    }

    public async Task<bool> SignOutEverywhereAsync(
        Guid id,
        Guid newSecurityStamp,
        CancellationToken cancellationToken)
    {
        return await UpdateAsync(id, account => account.SignOutEverywhere(newSecurityStamp), cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        var deleted = await dbContext.Accounts
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<AccountPage> GetUsersPageAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _readerDbContextFactory.CreateDbContextAsync(cancellationToken);

        var users = dbContext.Accounts
            .AsNoTracking()
            .Where(x => x.Role == AccountRole.User);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLike(search.Trim())}%";

            users = users.Where(x => EF.Functions.Like(x.Username, pattern, LikeEscape.ToString()));
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var items = await users
            .OrderBy(x => x.Username)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AccountPage(items, totalCount);
    }

    private async Task<bool> UpdateAsync(Guid id, Action<Account> action, CancellationToken cancellationToken)
    {
        await using var dbContext = await _writerDbContextFactory.CreateDbContextAsync(cancellationToken);

        var account = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (account is null)
        {
            return false;
        }

        action(account);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static string EscapeLike(string value)
    {
        return value
            .Replace(LikeEscape.ToString(), $"{LikeEscape}{LikeEscape}")
            .Replace("%", $"{LikeEscape}%")
            .Replace("_", $"{LikeEscape}_");
    }
}
