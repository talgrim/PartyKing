using PartyKing.Application.Accounts.Services;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Repositories;

namespace PartyKing.Application.Tests;

/// <summary>An in-memory accounts port that keeps the rules of the real one: usernames are unique whatever the case.</summary>
internal sealed class FakeAccountsRepository : IAccountsRepository
{
    public List<Account> Accounts { get; } = [];

    public Task<bool> AnyAdminAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.Any(x => x.Role == AccountRole.Admin));

    public Task<Account?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.FirstOrDefault(x => string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase)));

    public Task<Account?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.FirstOrDefault(x => x.Id == id));

    public Task<bool> TryAddAsync(Account account, CancellationToken cancellationToken)
    {
        if (Accounts.Any(x => string.Equals(x.Username, account.Username, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(false);
        }

        Accounts.Add(account);

        return Task.FromResult(true);
    }

    public Task<bool> ChangePasswordAsync(Guid id, string passwordHash, Guid newSecurityStamp, CancellationToken cancellationToken)
    {
        var account = Accounts.FirstOrDefault(x => x.Id == id);
        account?.ChangePassword(passwordHash, newSecurityStamp);

        return Task.FromResult(account is not null);
    }

    public Task<bool> SignOutEverywhereAsync(Guid id, Guid newSecurityStamp, CancellationToken cancellationToken)
    {
        var account = Accounts.FirstOrDefault(x => x.Id == id);
        account?.SignOutEverywhere(newSecurityStamp);

        return Task.FromResult(account is not null);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.RemoveAll(x => x.Id == id) > 0);

    public int? LastPage { get; private set; }
    public int? LastPageSize { get; private set; }

    public Task<AccountPage> GetUsersPageAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        LastPage = page;
        LastPageSize = pageSize;

        var users = Accounts
            .Where(x => x.Role == AccountRole.User)
            .Where(x => string.IsNullOrWhiteSpace(search) || x.Username.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(new AccountPage(users.Skip((page - 1) * pageSize).Take(pageSize).ToList(), users.Count));
    }
}

/// <summary>The real hasher takes 100 000+ PBKDF2 iterations a hash, which a unit test of the flow does not need.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    private const string Prefix = "fake$";

    public int Hashes { get; private set; }
    public List<int> Iterations { get; } = [];

    public static string HashOf(string password) => Prefix + password;

    public string Hash(string password, int iterations = PasswordHasher.AdminIterations)
    {
        Hashes++;
        Iterations.Add(iterations);

        return HashOf(password);
    }

    public bool Verify(string password, string hash) => hash == HashOf(password);

    public bool IsHash(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);
}
