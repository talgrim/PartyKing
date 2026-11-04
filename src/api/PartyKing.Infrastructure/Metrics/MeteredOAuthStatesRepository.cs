using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Repositories;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Infrastructure.Metrics;

internal sealed class MeteredOAuthStatesRepository : MeteredRepository, IOAuthStatesRepository
{
    private readonly IOAuthStatesRepository _inner;

    public MeteredOAuthStatesRepository(IOAuthStatesRepository inner, RepositoryMetrics metrics)
        : base(nameof(OAuthStatesRepository), metrics)
    {
        _inner = inner;
    }

    public Task AddAsync(OAuthState state, DateTime now, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(AddAsync), () => _inner.AddAsync(state, now, cancellationToken));
    }

    public Task<OAuthState?> TakeAsync(string id, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(TakeAsync), () => _inner.TakeAsync(id, cancellationToken));
    }
}
