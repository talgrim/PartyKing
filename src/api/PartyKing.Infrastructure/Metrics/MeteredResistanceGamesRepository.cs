using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Infrastructure.Metrics;

internal sealed class MeteredResistanceGamesRepository
    : MeteredLobbyGamesRepository<ResistanceGame, ResistancePlayer>, IResistanceGamesRepository
{
    private readonly IResistanceGamesRepository _inner;

    public MeteredResistanceGamesRepository(IResistanceGamesRepository inner, RepositoryMetrics metrics)
        : base(nameof(ResistanceGamesRepository), inner, metrics)
    {
        _inner = inner;
    }

    public Task<ResistanceStartResult> StartAsync(
        Guid sessionId, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(StartAsync), () => _inner.StartAsync(sessionId, startedAt, cancellationToken));
    }
}
