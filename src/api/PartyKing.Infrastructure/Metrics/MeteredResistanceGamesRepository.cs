using PartyKing.Domain.Repositories;
using PartyKing.Domain.Entities.Games.Resistance;
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

    public Task<ResistanceLobbyState?> GetLobbyAsync(
        Guid sessionId, Guid? playerId, Guid? playerStamp, CancellationToken cancellationToken)
    {
        return MeasureAsync(
            nameof(GetLobbyAsync), () => _inner.GetLobbyAsync(sessionId, playerId, playerStamp, cancellationToken));
    }
}
