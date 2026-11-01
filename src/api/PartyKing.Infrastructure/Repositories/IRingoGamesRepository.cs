using PartyKing.Domain.Entities.Games;

namespace PartyKing.Infrastructure.Repositories;

/// <summary>
/// The Ringo game of a session. Its actions are Domain methods run through
/// <see cref="ILobbyGamesRepository{TGame,TPlayer}.UpdateAsync{TResult}"/>.
/// </summary>
public interface IRingoGamesRepository : ILobbyGamesRepository<RingoGame, RingoPlayer>
{
}
