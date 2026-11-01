using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Domain.Repositories;

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

    /// <summary>
    /// Runs a game action on the session's game, loaded with everything it needs, and saves the changes when the
    /// result says it <see cref="IGameUpdateResult.Changed"/> something. Returns <paramref name="noGame"/> without
    /// running the action when the session has no game. <paramref name="operation"/> names the action in the metrics.
    /// </summary>
    Task<TResult> UpdateAsync<TResult>(
        Guid sessionId,
        string operation,
        TResult noGame,
        Func<TGame, TResult> action,
        CancellationToken cancellationToken)
        where TResult : IGameUpdateResult;

    /// <summary>Deletes the session's game; false when there was none.</summary>
    Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken);
}
