using PartyKing.Domain.Entities;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Infrastructure.Metrics;

internal sealed class MeteredAdminAccountsRepository : MeteredRepository, IAdminAccountsRepository
{
    private readonly IAdminAccountsRepository _inner;

    public MeteredAdminAccountsRepository(IAdminAccountsRepository inner, RepositoryMetrics metrics)
        : base(nameof(AdminAccountsRepository), metrics)
    {
        _inner = inner;
    }

    public Task<bool> AnyAsync(CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(AnyAsync), () => _inner.AnyAsync(cancellationToken));
    }

    public Task<AdminAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetByUsernameAsync), () => _inner.GetByUsernameAsync(username, cancellationToken));
    }

    public Task<AdminAccount?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetAsync), () => _inner.GetAsync(id, cancellationToken));
    }

    public Task AddAsync(AdminAccount account, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(AddAsync), () => _inner.AddAsync(account, cancellationToken));
    }
}
