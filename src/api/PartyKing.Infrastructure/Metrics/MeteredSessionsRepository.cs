using PartyKing.Domain.Entities;
using PartyKing.Domain.Enums;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Infrastructure.Metrics;

internal sealed class MeteredSessionsRepository : MeteredRepository, ISessionsRepository
{
    private readonly ISessionsRepository _inner;

    public MeteredSessionsRepository(ISessionsRepository inner, RepositoryMetrics metrics)
        : base(nameof(SessionsRepository), metrics)
    {
        _inner = inner;
    }

    public Task<bool> TryAddAsync(Session session, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(TryAddAsync), () => _inner.TryAddAsync(session, cancellationToken));
    }

    public Task<IReadOnlyList<Session>> GetAllAsync(CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetAllAsync), () => _inner.GetAllAsync(cancellationToken));
    }

    public Task<Session?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetAsync), () => _inner.GetAsync(id, cancellationToken));
    }

    public Task<bool> RenameAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(RenameAsync), () => _inner.RenameAsync(id, name, cancellationToken));
    }

    public Task<bool> UpdatePasswordHashAsync(Guid id, string passwordHash, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(UpdatePasswordHashAsync), () => _inner.UpdatePasswordHashAsync(id, passwordHash, cancellationToken));
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(DeleteAsync), () => _inner.DeleteAsync(id, cancellationToken));
    }

    public Task<SessionClaimResult> TryClaimAsync(Guid id, string profileId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(TryClaimAsync), () => _inner.TryClaimAsync(id, profileId, cancellationToken));
    }

    public Task<bool> ReleaseAsync(Guid id, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(ReleaseAsync), () => _inner.ReleaseAsync(id, cancellationToken));
    }
}
