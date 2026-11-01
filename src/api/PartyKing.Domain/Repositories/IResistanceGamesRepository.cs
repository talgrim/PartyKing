using PartyKing.Domain.Entities.Games;

namespace PartyKing.Domain.Repositories;

/// <summary>
/// The Resistance game of a session. Its actions are Domain methods run through
/// <see cref="ILobbyGamesRepository{TGame,TPlayer}.UpdateAsync{TResult}"/>.
/// </summary>
public interface IResistanceGamesRepository : ILobbyGamesRepository<ResistanceGame, ResistancePlayer>
{
}
