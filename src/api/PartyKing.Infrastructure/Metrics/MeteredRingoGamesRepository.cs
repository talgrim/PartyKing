using PartyKing.Domain.Repositories;
using PartyKing.Domain.Entities.Games.Base;
using PartyKing.Domain.Entities.Games.Ringo;
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
