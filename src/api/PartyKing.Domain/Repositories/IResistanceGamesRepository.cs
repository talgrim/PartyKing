using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Domain.Repositories;

/// <summary>
/// The Resistance game of a session. Its actions are Domain methods run through
/// <see cref="ILobbyGamesRepository{TGame,TPlayer}.UpdateAsync{TResult}"/>.
/// </summary>
public interface IResistanceGamesRepository : ILobbyGamesRepository<ResistanceGame, ResistancePlayer>
{
    /// <summary>
    /// The few facts the lobby shows, read with one query that loads no game: every tab asks for them, and the
    /// whole game graph is far more than they need. Null when the session has no game. <paramref name="playerId"/>
    /// is the player cookie's id (null for a caller without one); it counts as joined only when that player is in the
    /// game, the same rule as <c>GameLobby.IsPlayer</c> (the host and admins are not players by their role).
    /// </summary>
    Task<ResistanceLobbyState?> GetLobbyAsync(Guid sessionId, Guid? playerId, CancellationToken cancellationToken);
}
