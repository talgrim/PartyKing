using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Repositories;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Infrastructure.Metrics;

internal sealed class MeteredAccountsRepository : MeteredRepository, IAccountsRepository
{
    private readonly IAccountsRepository _inner;

    public MeteredAccountsRepository(IAccountsRepository inner, RepositoryMetrics metrics)
        : base(nameof(AccountsRepository), metrics)
    {
        _inner = inner;
    }

    public Task<bool> AnyAdminAsync(CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(AnyAdminAsync), () => _inner.AnyAdminAsync(cancellationToken));
    }

    public Task<Account?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetByUsernameAsync), () => _inner.GetByUsernameAsync(username, cancellationToken));
    }

    public Task<Account?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetAsync), () => _inner.GetAsync(id, cancellationToken));
    }

    public Task<bool> TryAddAsync(Account account, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(TryAddAsync), () => _inner.TryAddAsync(account, cancellationToken));
    }

    public Task<bool> ChangePasswordAsync(
        Guid id,
        string passwordHash,
        Guid newSecurityStamp,
        CancellationToken cancellationToken)
    {
        return MeasureAsync(
            nameof(ChangePasswordAsync),
            () => _inner.ChangePasswordAsync(id, passwordHash, newSecurityStamp, cancellationToken));
    }

    public Task<bool> SignOutEverywhereAsync(Guid id, Guid newSecurityStamp, CancellationToken cancellationToken)
    {
        return MeasureAsync(
            nameof(SignOutEverywhereAsync),
            () => _inner.SignOutEverywhereAsync(id, newSecurityStamp, cancellationToken));
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(DeleteAsync), () => _inner.DeleteAsync(id, cancellationToken));
    }

    public Task<AccountPage> GetUsersPageAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        return MeasureAsync(
            nameof(GetUsersPageAsync),
            () => _inner.GetUsersPageAsync(search, page, pageSize, cancellationToken));
    }
}
