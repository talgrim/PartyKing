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

    /// <summary>What playing or confirming a plot card answers; the rules behind it are the repository's, tested there.</summary>
    public ResistancePlayCardStatus PlayCardStatus { get; set; } = ResistancePlayCardStatus.Done;

    public Task<ResistancePlayCardResult> PlayPlotCardAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, Guid targetPlayerId, CancellationToken cancellationToken) =>
        Task.FromResult(new ResistancePlayCardResult(PlayCardStatus, _games.GetValueOrDefault(sessionId)));

    public Task<ResistancePlayCardResult> TakeResponsibilityAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, Guid targetPlayerId, bool? takeOpinionMaker,
        CancellationToken cancellationToken) =>
        Task.FromResult(new ResistancePlayCardResult(PlayCardStatus, _games.GetValueOrDefault(sessionId)));

    public Task<ResistancePlayCardResult> TakeLeadAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, CancellationToken cancellationToken) =>
        Task.FromResult(new ResistancePlayCardResult(PlayCardStatus, _games.GetValueOrDefault(sessionId)));

    public Task<ResistancePlayCardResult> PeekMissionCardAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, Guid targetPlayerId, CancellationToken cancellationToken) =>
        Task.FromResult(new ResistancePlayCardResult(PlayCardStatus, _games.GetValueOrDefault(sessionId)));

    public Task<ResistancePlayCardResult> PlayNoConfidenceAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, CancellationToken cancellationToken) =>
        Task.FromResult(new ResistancePlayCardResult(PlayCardStatus, _games.GetValueOrDefault(sessionId)));

    public Task<ResistancePlayCardResult> PassLeaderWindowAsync(
        Guid sessionId, Guid? callerPlayerId, CancellationToken cancellationToken) =>
        Task.FromResult(new ResistancePlayCardResult(PlayCardStatus, _games.GetValueOrDefault(sessionId)));

    public Task<ResistancePlayCardResult> ConfirmPlotCardAsync(
        Guid sessionId, Guid? callerPlayerId, int cardId, CancellationToken cancellationToken) =>
        Task.FromResult(new ResistancePlayCardResult(PlayCardStatus, _games.GetValueOrDefault(sessionId)));

    public Task<ResistanceDealResult> DealPlotCardsAsync(
        Guid sessionId, Guid? callerPlayerId, IReadOnlyList<PlotCardAssignment> assignments,
        CancellationToken cancellationToken)
    {
        _games.TryGetValue(sessionId, out var game);
        var status = ResistanceDealStatus.Dealt;
        var toDeal = game is null ? [] : ResistanceRoles.CardsToDeal(game).ToDictionary(x => x.CardId);
        if (game is null)
        {
            status = ResistanceDealStatus.NoGame;
        }
        else if (!game.IsStarted)
        {
            status = ResistanceDealStatus.NotStarted;
        }
        else if (game.Phase == ResistancePhase.GameOver)
        {
            status = ResistanceDealStatus.GameOver;
        }
        else if (callerPlayerId is null || callerPlayerId != game.LeaderPlayerId)
        {
            status = ResistanceDealStatus.NotLeader;
        }
        else if (game.Phase != ResistancePhase.DealingCards)
        {
            status = ResistanceDealStatus.NotDealing;
        }
        else if (assignments.Any(x => !toDeal.ContainsKey(x.CardId))
                 || assignments.Select(x => x.CardId).Distinct().Count() != assignments.Count)
        {
            status = ResistanceDealStatus.UnknownCard;
        }
        else if (assignments.Any(x => x.PlayerId == game.LeaderPlayerId || game.Players.All(p => p.PlayerId != x.PlayerId)))
        {
            status = ResistanceDealStatus.InvalidReceiver;
        }
        else if (assignments.Count != toDeal.Count)
        {
            status = ResistanceDealStatus.Incomplete;
        }
        else
        {
            foreach (var assignment in assignments)
            {
                toDeal[assignment.CardId].Location = ResistancePlotCardLocation.Hand;
                toDeal[assignment.CardId].HolderPlayerId = assignment.PlayerId;
            }

            foreach (var kept in game!.PlotCards.Where(x => x.Location == ResistancePlotCardLocation.Drawn))
            {
                kept.Location = ResistancePlotCardLocation.Hand;
            }

            game.Phase = ResistancePhase.SelectingTeam;
        }

        return Task.FromResult(new ResistanceDealResult(status, game));
    }

    private static Task<LobbyResult<ResistanceGame>> Result(LobbyStatus status, ResistanceGame? game) =>
        Task.FromResult(new LobbyResult<ResistanceGame>(status, game));
}
