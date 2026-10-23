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

    public Task<string> CreateAsync(Guid sessionId, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(CreateAsync), () => _inner.CreateAsync(sessionId, lifetime, cancellationToken));
    }

    public Task<Guid?> ConsumeAsync(string state, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(ConsumeAsync), () => _inner.ConsumeAsync(state, cancellationToken));
    }
}
