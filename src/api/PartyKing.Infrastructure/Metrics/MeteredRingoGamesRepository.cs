using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Infrastructure.Metrics;

internal sealed class MeteredRingoGamesRepository
    : MeteredLobbyGamesRepository<RingoGame, RingoPlayer>, IRingoGamesRepository
{
    public MeteredRingoGamesRepository(IRingoGamesRepository inner, RepositoryMetrics metrics)
        : base(nameof(RingoGamesRepository), inner, metrics)
    {
    }
}
