using PartyKing.Domain.Entities.Demo;
using PartyKing.Domain.Repositories;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Infrastructure.Metrics;

internal sealed class MeteredDemoSessionsRepository : MeteredRepository, IDemoSessionsRepository
{
    private readonly IDemoSessionsRepository _inner;

    public MeteredDemoSessionsRepository(IDemoSessionsRepository inner, RepositoryMetrics metrics)
        : base(nameof(DemoSessionsRepository), metrics)
    {
        _inner = inner;
    }

    public Task AddAsync(DemoSession demoSession, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(AddAsync), () => _inner.AddAsync(demoSession, cancellationToken));
    }

    public Task<DemoSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetAsync), () => _inner.GetAsync(sessionId, cancellationToken));
    }

    public Task<int> CountAsync(CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(CountAsync), () => _inner.CountAsync(cancellationToken));
    }

    public Task<IReadOnlyList<DemoSession>> ListExpiredAsync(DateTime now, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(ListExpiredAsync), () => _inner.ListExpiredAsync(now, cancellationToken));
    }
}
