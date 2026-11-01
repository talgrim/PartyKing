using PartyKing.Domain.Repositories;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Domain.Entities.Games.Base;
using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Application.Tests;

internal sealed class FakeRulesProvider : IResistanceRulesProvider
{
    public ResistanceRules Rules { get; set; } = ResistanceRules.Default();

    public ResistanceRules Current() => new()
    {
        PlayerCounts = new Dictionary<int, ResistanceCountRules>(Rules.PlayerCounts),
    };
}

internal sealed class FakeResistanceGamesRepository : IResistanceGamesRepository
{
    private readonly Dictionary<Guid, ResistanceGame> _games = [];

    public Task<ResistanceGame?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(_games.GetValueOrDefault(sessionId));

    public Task<bool> TryCreateAsync(ResistanceGame game, CancellationToken cancellationToken) =>
        Task.FromResult(_games.TryAdd(game.SessionId, game));

    public Task<TResult> UpdateAsync<TResult>(
        Guid sessionId,
        string operation,
        TResult noGame,
        Func<ResistanceGame, TResult> action,
        CancellationToken cancellationToken)
        where TResult : IGameUpdateResult
    {
        return Task.FromResult(_games.TryGetValue(sessionId, out var game) ? action(game) : noGame);
    }

    public Task<ResistanceGame?> OpenAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var game = _games.GetValueOrDefault(sessionId);
        game?.Open();

        return Task.FromResult(game);
    }

    public Task<LobbyResult<ResistanceGame>> JoinAsync(
        Guid sessionId, ResistancePlayer player, CancellationToken cancellationToken)
    {
        return _games.TryGetValue(sessionId, out var game)
            ? Result(game.Join(player), game)
            : Result(LobbyStatus.NoGame, null);
    }

    public Task<LobbyResult<ResistanceGame>> KickAsync(
        Guid sessionId, Guid playerId, CancellationToken cancellationToken)
    {
        return _games.TryGetValue(sessionId, out var game)
            ? Result(game.Kick(playerId).Status, game)
            : Result(LobbyStatus.NoGame, null);
    }

    public Task<LobbyResult<ResistanceGame>> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken)
    {
        return _games.TryGetValue(sessionId, out var game)
            ? Result(game.Reorder(playerIds), game)
            : Result(LobbyStatus.NoGame, null);
    }

    public Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(_games.Remove(sessionId));

    private static Task<LobbyResult<ResistanceGame>> Result(LobbyStatus status, ResistanceGame? game) =>
        Task.FromResult(new LobbyResult<ResistanceGame>(status, game));
}
