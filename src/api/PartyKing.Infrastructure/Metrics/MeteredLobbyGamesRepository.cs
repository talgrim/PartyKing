using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Results;

namespace PartyKing.Infrastructure.Metrics;

/// <summary>Times the calls every lobby game repository has; a game's decorator adds its own.</summary>
internal abstract class MeteredLobbyGamesRepository<TGame, TPlayer> : MeteredRepository,
    ILobbyGamesRepository<TGame, TPlayer>
    where TGame : LobbyGame<TPlayer>
    where TPlayer : LobbyPlayer
{
    private readonly ILobbyGamesRepository<TGame, TPlayer> _inner;

    protected MeteredLobbyGamesRepository(
        string repositoryName, ILobbyGamesRepository<TGame, TPlayer> inner, RepositoryMetrics metrics)
        : base(repositoryName, metrics)
    {
        _inner = inner;
    }

    public Task<TGame?> GetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetAsync), () => _inner.GetAsync(sessionId, cancellationToken));
    }

    public Task<bool> TryCreateAsync(TGame game, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(TryCreateAsync), () => _inner.TryCreateAsync(game, cancellationToken));
    }

    public Task<TGame?> OpenAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(OpenAsync), () => _inner.OpenAsync(sessionId, cancellationToken));
    }

    public Task<LobbyResult<TGame>> JoinAsync(Guid sessionId, TPlayer player, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(JoinAsync), () => _inner.JoinAsync(sessionId, player, cancellationToken));
    }

    public Task<LobbyResult<TGame>> KickAsync(Guid sessionId, Guid playerId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(KickAsync), () => _inner.KickAsync(sessionId, playerId, cancellationToken));
    }

    public Task<LobbyResult<TGame>> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(ReorderAsync), () => _inner.ReorderAsync(sessionId, playerIds, cancellationToken));
    }

    public Task<TResult> UpdateAsync<TResult>(
        Guid sessionId,
        string operation,
        TResult noGame,
        Func<TGame, TResult> action,
        CancellationToken cancellationToken)
        where TResult : IGameUpdateResult
    {
        return MeasureAsync(operation, () => _inner.UpdateAsync(sessionId, operation, noGame, action, cancellationToken));
    }

    public Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(DeleteAsync), () => _inner.DeleteAsync(sessionId, cancellationToken));
    }
}
