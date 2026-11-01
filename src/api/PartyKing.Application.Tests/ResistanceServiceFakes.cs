using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Results;

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
        if (game is not null)
        {
            game.IsOpen = true;
        }

        return Task.FromResult(game);
    }

    public Task<LobbyResult<ResistanceGame>> JoinAsync(
        Guid sessionId, ResistancePlayer player, CancellationToken cancellationToken)
    {
        if (!_games.TryGetValue(sessionId, out var game))
        {
            return Result(LobbyStatus.NoGame, null);
        }

        if (!game.IsOpen)
        {
            return Result(LobbyStatus.NotOpen, game);
        }

        if (game.IsStarted)
        {
            return Result(LobbyStatus.Started, game);
        }

        if (game.Players.Any(x => x.NormalizedName == player.NormalizedName))
        {
            return Result(LobbyStatus.NameTaken, game);
        }

        if (game.Players.Count >= ResistanceGame.MaxPlayers)
        {
            return Result(LobbyStatus.Full, game);
        }

        player.SessionId = sessionId;
        player.Order = game.Players.Count;
        game.Players.Add(player);

        return Result(LobbyStatus.Done, game);
    }

    public Task<LobbyResult<ResistanceGame>> KickAsync(
        Guid sessionId, Guid playerId, CancellationToken cancellationToken)
    {
        if (!_games.TryGetValue(sessionId, out var game))
        {
            return Result(LobbyStatus.NoGame, null);
        }

        if (game.IsStarted)
        {
            return Result(LobbyStatus.Started, game);
        }

        var kicked = game.Players.FirstOrDefault(x => x.PlayerId == playerId);
        if (kicked is null)
        {
            return Result(LobbyStatus.NoPlayer, game);
        }

        game.Players.Remove(kicked);
        var order = 0;
        foreach (var player in game.Players.OrderBy(x => x.Order))
        {
            player.Order = order++;
        }

        return Result(LobbyStatus.Done, game);
    }

    public Task<LobbyResult<ResistanceGame>> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken)
    {
        if (!_games.TryGetValue(sessionId, out var game))
        {
            return Result(LobbyStatus.NoGame, null);
        }

        if (game.IsStarted)
        {
            return Result(LobbyStatus.Started, game);
        }

        if (playerIds.Count != game.Players.Count || game.Players.Any(x => !playerIds.Contains(x.PlayerId)))
        {
            return Result(LobbyStatus.OrderMismatch, game);
        }

        foreach (var player in game.Players)
        {
            player.Order = playerIds.ToList().IndexOf(player.PlayerId);
        }

        return Result(LobbyStatus.Done, game);
    }

    public Task<bool> DeleteAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(_games.Remove(sessionId));

    private static Task<LobbyResult<ResistanceGame>> Result(LobbyStatus status, ResistanceGame? game) =>
        Task.FromResult(new LobbyResult<ResistanceGame>(status, game));
}
