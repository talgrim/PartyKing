using PartyKing.Application.Events;
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
    private readonly IDomainEventDispatcher _dispatcher;

    public FakeResistanceGamesRepository(IDomainEventDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task<ResistanceGame?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(_games.GetValueOrDefault(sessionId));

    public Task<bool> TryCreateAsync(ResistanceGame game, CancellationToken cancellationToken) =>
        Task.FromResult(_games.TryAdd(game.SessionId, game));

    public async Task<TResult> UpdateAsync<TResult>(
        Guid sessionId,
        string operation,
        TResult noGame,
        Func<ResistanceGame, TResult> action,
        CancellationToken cancellationToken)
        where TResult : IGameUpdateResult
    {
        if (!_games.TryGetValue(sessionId, out var game))
        {
            return noGame;
        }

        var result = action(game);
        if (result.Changed)
        {
            await _dispatcher.DispatchAsync(game);
        }

        return result;
    }

    public async Task<ResistanceGame?> OpenAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var game = _games.GetValueOrDefault(sessionId);
        if (game?.Open() == true)
        {
            await _dispatcher.DispatchAsync(game);
        }

        return game;
    }

    public async Task<LobbyResult<ResistanceGame>> JoinAsync(
        Guid sessionId, ResistancePlayer player, CancellationToken cancellationToken)
    {
        return _games.TryGetValue(sessionId, out var game)
            ? await ResultAsync(game.Join(player), game)
            : new LobbyResult<ResistanceGame>(LobbyStatus.NoGame, null);
    }

    public async Task<LobbyResult<ResistanceGame>> KickAsync(
        Guid sessionId, Guid playerId, CancellationToken cancellationToken)
    {
        return _games.TryGetValue(sessionId, out var game)
            ? await ResultAsync(game.Kick(playerId).Status, game)
            : new LobbyResult<ResistanceGame>(LobbyStatus.NoGame, null);
    }

    public async Task<LobbyResult<ResistanceGame>> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken)
    {
        return _games.TryGetValue(sessionId, out var game)
            ? await ResultAsync(game.Reorder(playerIds), game)
            : new LobbyResult<ResistanceGame>(LobbyStatus.NoGame, null);
    }

    public Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(_games.Remove(sessionId));

    private async Task<LobbyResult<ResistanceGame>> ResultAsync(LobbyStatus status, ResistanceGame game)
    {
        if (status == LobbyStatus.Done)
        {
            await _dispatcher.DispatchAsync(game);
        }

        return new LobbyResult<ResistanceGame>(status, game);
    }
}
