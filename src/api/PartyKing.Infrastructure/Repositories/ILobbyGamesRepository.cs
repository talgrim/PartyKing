using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Infrastructure.Repositories;

/// <summary>What every game with a lobby (one game per session, opened by an admin, joined by name) stores.</summary>
public interface ILobbyGamesRepository<TGame, TPlayer>
    where TGame : LobbyGame<TPlayer>
    where TPlayer : LobbyPlayer
{
    Task<TGame?> GetAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Creates the session's game; false when it already has one.</summary>
    Task<bool> TryCreateAsync(TGame game, CancellationToken cancellationToken);

    /// <summary>Opens the game to guests (idempotent); null when the session has no game.</summary>
    Task<TGame?> OpenAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Adds the player at the end of the player order, see <see cref="LobbyStatus"/>.</summary>
    Task<LobbyResult<TGame>> JoinAsync(Guid sessionId, TPlayer player, CancellationToken cancellationToken);

    /// <summary>Removes a player.</summary>
    Task<LobbyResult<TGame>> KickAsync(Guid sessionId, Guid playerId, CancellationToken cancellationToken);

    /// <summary>Sets the player order to the given ids, which must be exactly the game's players.</summary>
    Task<LobbyResult<TGame>> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken);

    /// <summary>Deletes the session's game; false when there was none.</summary>
    Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken);
}
