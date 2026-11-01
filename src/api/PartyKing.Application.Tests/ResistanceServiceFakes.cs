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

    public Task<ResistanceRulesResult> RefreshRulesAsync(
        Guid sessionId, ResistanceRules rules, CancellationToken cancellationToken)
    {
        var game = _games.GetValueOrDefault(sessionId);
        if (game is null)
        {
            return Task.FromResult(new ResistanceRulesResult(ResistanceRulesStatus.NoGame, null));
        }

        if (game.IsOpen || game.IsStarted)
        {
            return Task.FromResult(new ResistanceRulesResult(ResistanceRulesStatus.NotEditable, game));
        }

        rules.PlotCardsEnabled = game.Rules.PlotCardsEnabled;
        game.Rules = rules;
        return Task.FromResult(new ResistanceRulesResult(ResistanceRulesStatus.Refreshed, game));
    }

    public Task<ResistanceRulesResult> SetPlotCardsAsync(
        Guid sessionId, bool enabled, CancellationToken cancellationToken)
    {
        var game = _games.GetValueOrDefault(sessionId);
        if (game is null)
        {
            return Task.FromResult(new ResistanceRulesResult(ResistanceRulesStatus.NoGame, null));
        }

        if (game.IsStarted)
        {
            return Task.FromResult(new ResistanceRulesResult(ResistanceRulesStatus.NotEditable, game));
        }

        game.Rules.PlotCardsEnabled = enabled;
        return Task.FromResult(new ResistanceRulesResult(ResistanceRulesStatus.Refreshed, game));
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

    public Task<ResistanceStartResult> StartAsync(
        Guid sessionId, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        var status = ResistanceStartStatus.Started;
        _games.TryGetValue(sessionId, out var game);
        if (game is null)
        {
            status = ResistanceStartStatus.NoGame;
        }
        else if (game.IsStarted)
        {
            status = ResistanceStartStatus.AlreadyStarted;
        }
        else if (!game.IsOpen)
        {
            status = ResistanceStartStatus.NotOpen;
        }
        else if (game.Players.Count < ResistanceGame.MinPlayers)
        {
            status = ResistanceStartStatus.NotEnoughPlayers;
        }
        else
        {
            ResistanceRoles.Deal(game, new Random(1));
            game.StartedAt = startedAt;
        }

        return Task.FromResult(new ResistanceStartResult(status, game));
    }

    public Task<ResistanceRestartResult> RestartAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        var status = ResistanceRestartStatus.Restarted;
        _games.TryGetValue(sessionId, out var game);
        if (game is null)
        {
            status = ResistanceRestartStatus.NoGame;
        }
        else if (!game.IsStarted)
        {
            status = ResistanceRestartStatus.NotStarted;
        }
        else if (game.Phase != ResistancePhase.GameOver)
        {
            status = ResistanceRestartStatus.NotOver;
        }
        else
        {
            ResistanceRoles.Reset(game);
            game.StartedAt = null;
        }

        return Task.FromResult(new ResistanceRestartResult(status, game));
    }

    public Task<ResistanceTeamResult> SelectTeamAsync(
        Guid sessionId, Guid? callerPlayerId, IReadOnlyList<Guid> playerIds, CancellationToken cancellationToken)
    {
        _games.TryGetValue(sessionId, out var game);
        var status = ResistanceTeamStatus.Selected;
        if (game is null)
        {
            status = ResistanceTeamStatus.NoGame;
        }
        else if (!game.IsStarted)
        {
            status = ResistanceTeamStatus.NotStarted;
        }
        else if (game.Phase == ResistancePhase.GameOver)
        {
            status = ResistanceTeamStatus.GameOver;
        }
        else if (callerPlayerId is null || callerPlayerId != game.LeaderPlayerId)
        {
            status = ResistanceTeamStatus.NotLeader;
        }
        else if (game.Phase == ResistancePhase.DealingCards)
        {
            status = ResistanceTeamStatus.CardsNotDealt;
        }
        else if (game.Phase != ResistancePhase.SelectingTeam)
        {
            status = ResistanceTeamStatus.AlreadySubmitted;
        }
        else if (playerIds.Count != game.Rules.TeamSize(game.Players.Count, game.MissionNumber)
            || playerIds.Distinct().Count() != playerIds.Count)
        {
            status = ResistanceTeamStatus.WrongSize;
        }
        else if (playerIds.Any(id => game.Players.All(x => x.PlayerId != id)))
        {
            status = ResistanceTeamStatus.UnknownPlayer;
        }
        else
        {
            foreach (var player in game.Players)
            {
                player.IsOnTeam = playerIds.Contains(player.PlayerId);
                player.Vote = null;
                player.MissionCard = null;
            }

            game.Phase = ResistancePhase.Voting;
        }

        return Task.FromResult(new ResistanceTeamResult(status, game));
    }

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

    public Task<ResistanceVoteResult> VoteAsync(
        Guid sessionId, Guid? callerPlayerId, bool approve, CancellationToken cancellationToken)
    {
        _games.TryGetValue(sessionId, out var game);
        var voter = game?.Players.FirstOrDefault(x => x.PlayerId == callerPlayerId);
        var status = ResistanceVoteStatus.Voted;
        if (game is null)
        {
            status = ResistanceVoteStatus.NoGame;
        }
        else if (!game.IsStarted)
        {
            status = ResistanceVoteStatus.NotStarted;
        }
        else if (game.Phase == ResistancePhase.GameOver)
        {
            status = ResistanceVoteStatus.GameOver;
        }
        else if (voter is null)
        {
            status = ResistanceVoteStatus.NotPlayer;
        }
        else if (game.Phase != ResistancePhase.Voting)
        {
            status = ResistanceVoteStatus.NotVoting;
        }
        else
        {
            voter.Vote = approve;
            if (game.Players.All(x => x.Vote is not null))
            {
                ResistanceRoles.ResolveVotes(game);
            }
        }

        return Task.FromResult(new ResistanceVoteResult(status, game));
    }

    public Task<ResistanceMissionCardResult> PlayMissionCardAsync(
        Guid sessionId, Guid? callerPlayerId, bool success, CancellationToken cancellationToken)
    {
        _games.TryGetValue(sessionId, out var game);
        var player = game?.Players.FirstOrDefault(x => x.PlayerId == callerPlayerId);
        var status = ResistanceMissionStatus.Played;
        if (game is null)
        {
            status = ResistanceMissionStatus.NoGame;
        }
        else if (!game.IsStarted)
        {
            status = ResistanceMissionStatus.NotStarted;
        }
        else if (game.Phase == ResistancePhase.GameOver)
        {
            status = ResistanceMissionStatus.GameOver;
        }
        else if (player is null)
        {
            status = ResistanceMissionStatus.NotPlayer;
        }
        else if (game.Phase != ResistancePhase.PlayingMission)
        {
            status = ResistanceMissionStatus.NotPlaying;
        }
        else if (!player.IsOnTeam)
        {
            status = ResistanceMissionStatus.NotOnTeam;
        }
        else if (player.MissionCard is not null)
        {
            status = ResistanceMissionStatus.AlreadyPlayed;
        }
        else if (!success && player.Role == ResistanceRole.Resistance)
        {
            status = ResistanceMissionStatus.MustSucceed;
        }
        else
        {
            player.MissionCard = success;
            if (ResistanceRoles.AllCardsPlayed(game))
            {
                ResistanceRoles.ResolveMission(game);
            }
        }

        return Task.FromResult(new ResistanceMissionCardResult(status, game));
    }

    public Task<ResistanceContinueResult> ContinueAsync(
        Guid sessionId, Guid? callerPlayerId, bool isManager, CancellationToken cancellationToken)
    {
        _games.TryGetValue(sessionId, out var game);
        var status = ResistanceContinueStatus.Continued;
        if (game is null)
        {
            status = ResistanceContinueStatus.NoGame;
        }
        else if (!game.IsStarted)
        {
            status = ResistanceContinueStatus.NotStarted;
        }
        else if (game.Phase == ResistancePhase.GameOver)
        {
            status = ResistanceContinueStatus.GameOver;
        }
        else if (!isManager && (callerPlayerId is null || callerPlayerId != game.LeaderPlayerId))
        {
            status = ResistanceContinueStatus.NotAllowed;
        }
        else if (game.Phase != ResistancePhase.MissionResult)
        {
            status = ResistanceContinueStatus.NothingToContinue;
        }
        else
        {
            ResistanceRoles.PassLeadership(game);
        }

        return Task.FromResult(new ResistanceContinueResult(status, game));
    }

    private static Task<LobbyResult<ResistanceGame>> Result(LobbyStatus status, ResistanceGame? game) =>
        Task.FromResult(new LobbyResult<ResistanceGame>(status, game));
}
