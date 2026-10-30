using ErrorOr;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Games;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Games.Services;

public interface IResistanceService
{
    /// <summary>
    /// The session's game. NotFound when there is none, and for anyone but the host and admins while it is closed.
    /// Once it is open everyone who unlocked the session may watch it.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> GetAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>What a guest may know: whether the game is open and whether the caller joined. NotFound without a game.</summary>
    Task<ErrorOr<ResistanceLobbyDto>> GetLobbyAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>Host or admin: creates the game, closed to guests. Conflict when the session already has one.</summary>
    Task<ErrorOr<ResistanceGameDto>> CreateAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>Admin only: opens the game to guests (idempotent). NotFound without a game, Forbidden for the host.</summary>
    Task<ErrorOr<ResistanceGameDto>> OpenAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// Joins an open game and returns the new player's id (the caller's own one when they already joined), see
    /// <see cref="GameLobby{TGame,TPlayer}.JoinAsync"/>. Conflict as well when the game has 10 players.
    /// </summary>
    Task<ErrorOr<Guid>> JoinAsync(
        Guid sessionId, string? name, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// Host or admin: starts the open game once at least 5 players joined, which deals the roles, picks the first
    /// leader and locks the players. NotFound without a game, Conflict while it is closed, started or short of players.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> StartAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// Host or admin: starts a finished game over with the same players (new roles and leader, mission 1). NotFound
    /// without a game, Conflict before the start and while the game is still being played.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> RestartAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// The mission leader submits the team of the current mission: exactly <c>RequiredTeamSize</c> distinct players,
    /// which locks it. Forbidden for anyone but the leader (the host and admins get no override), NotFound without a
    /// game, Conflict before the start or once a team was submitted, Validation for a wrong size or unknown player.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> SelectTeamAsync(
        Guid sessionId, IReadOnlyList<Guid>? playerIds, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// A player votes on the submitted team, and may change the vote until the last player has voted. Forbidden for
    /// anyone who did not join (the host and admins too), NotFound without a game, Conflict before the start, when
    /// no team is up for a vote or when the game is over.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> VoteAsync(
        Guid sessionId, bool approve, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// A member of the approved team secretly plays Success (true) or Fail (false), once. Forbidden for anyone who did
    /// not join, who is not on the team, or is a member of the resistance playing Fail; Conflict before the start, when
    /// no approved team is playing, when the caller already played, and when the game is over.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> PlayMissionCardAsync(
        Guid sessionId, bool success, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// Moves on after a rejected team or a finished mission (next leader, new team, next mission). The leader, the host
    /// and admins may; Forbidden for other players, Conflict unless the votes rejected the team or the mission is over.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> ContinueAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// Host or admin: replaces the game's rules with the current server configuration. NotFound without a game,
    /// Conflict once the game is open or started.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> RefreshRulesAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>Host or admin: removes a player. NotFound for an unknown game or player.</summary>
    Task<ErrorOr<ResistanceGameDto>> KickAsync(
        Guid sessionId, Guid playerId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>Host or admin: sets the seating order. Conflict when the ids are not exactly the game's players.</summary>
    Task<ErrorOr<ResistanceGameDto>> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid>? playerIds, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>Host or admin: ends the game. NotFound when there is none.</summary>
    Task<ErrorOr<Deleted>> EndAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken);
}

internal class ResistanceService : IResistanceService
{
    private readonly IResistanceGamesRepository _games;
    private readonly IDateTimeProvider _clock;
    private readonly ISessionMusicNotifier _notifier;
    private readonly IResistanceRulesProvider _rulesProvider;
    private readonly GameLobby<ResistanceGame, ResistancePlayer> _lobby;

    public ResistanceService(
        ISessionService sessionService,
        ISessionsRepository sessions,
        IResistanceGamesRepository games,
        IDateTimeProvider clock,
        ISessionMusicNotifier notifier,
        IResistanceRulesProvider rulesProvider)
    {
        _games = games;
        _rulesProvider = rulesProvider;
        _clock = clock;
        _notifier = notifier;
        _lobby = new GameLobby<ResistanceGame, ResistancePlayer>(
            sessionService,
            sessions,
            games,
            clock,
            new ResistanceLobbyNotifier(notifier),
            new GameLobbyOptions("Resistance", ResistanceGame.MaxPlayers));
    }

    public async Task<ErrorOr<ResistanceGameDto>> GetAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        var authorization = await _lobby.AuthorizeAsync(sessionId, caller, cancellationToken);
        if (authorization.IsError && authorization.FirstError.Type != ErrorType.Forbidden)
        {
            return authorization.Errors;
        }

        var game = await _games.GetAsync(sessionId, cancellationToken);

        return game is null || (authorization.IsError && !game.IsOpen) ? _lobby.NoGame : ToDto(game, caller);
    }

    public async Task<ErrorOr<ResistanceLobbyDto>> GetLobbyAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        var game = await _games.GetAsync(sessionId, cancellationToken);

        return game is null
            ? _lobby.NoGame
            : new ResistanceLobbyDto(
                game.CreatedAt,
                game.IsOpen,
                game.IsStarted,
                GameLobby<ResistanceGame, ResistancePlayer>.IsPlayer(game, caller));
    }

    public async Task<ErrorOr<ResistanceGameDto>> CreateAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        if (await _lobby.AuthorizeAsync(sessionId, caller, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        var game = new ResistanceGame { SessionId = sessionId, CreatedAt = _clock.UtcNow, Rules = _rulesProvider.Current() };
        if (!await _games.TryCreateAsync(game, cancellationToken))
        {
            return Error.Conflict("Resistance.AlreadyCreated", "The session already has a Resistance game.");
        }

        await _notifier.ResistanceChangedAsync(sessionId, cancellationToken);

        return ToDto(game, caller);
    }

    public async Task<ErrorOr<ResistanceGameDto>> RefreshRulesAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        if (await _lobby.AuthorizeAsync(sessionId, caller, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        var result = await _games.RefreshRulesAsync(sessionId, _rulesProvider.Current(), cancellationToken);
        switch (result.Status)
        {
            case ResistanceRulesStatus.NoGame:
                return _lobby.NoGame;
            case ResistanceRulesStatus.NotEditable:
                return Error.Conflict(
                    "Resistance.RulesLocked", "The rules can only be refreshed before the game is opened.");
        }

        await _notifier.ResistanceChangedAsync(sessionId, cancellationToken);

        return ToDto(result.Game!, caller);
    }

    public async Task<ErrorOr<ResistanceGameDto>> OpenAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        var result = await _lobby.OpenAsync(sessionId, caller, cancellationToken);

        return result.IsError ? result.Errors : ToDto(result.Value, caller);
    }

    public async Task<ErrorOr<ResistanceGameDto>> StartAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        if (await _lobby.AuthorizeAsync(sessionId, caller, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        var result = await _games.StartAsync(sessionId, _clock.UtcNow, cancellationToken);
        switch (result.Status)
        {
            case ResistanceStartStatus.NoGame:
                return _lobby.NoGame;
            case ResistanceStartStatus.NotOpen:
                return Error.Conflict("Resistance.NotOpen", "The game is not open to guests yet.");
            case ResistanceStartStatus.AlreadyStarted:
                return Error.Conflict("Resistance.AlreadyStarted", "The game has already started.");
            case ResistanceStartStatus.NotEnoughPlayers:
                return Error.Conflict(
                    "Resistance.NotEnoughPlayers",
                    $"The game needs at least {ResistanceGame.MinPlayers} players to start.");
        }

        await _notifier.ResistanceChangedAsync(sessionId, cancellationToken);

        return ToDto(result.Game!, caller);
    }

    public async Task<ErrorOr<ResistanceGameDto>> RestartAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        if (await _lobby.AuthorizeAsync(sessionId, caller, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        var result = await _games.RestartAsync(sessionId, _clock.UtcNow, cancellationToken);
        switch (result.Status)
        {
            case ResistanceRestartStatus.NoGame:
                return _lobby.NoGame;
            case ResistanceRestartStatus.NotStarted:
                return Error.Conflict("Resistance.NotStarted", "The game has not started yet.");
            case ResistanceRestartStatus.NotOver:
                return Error.Conflict("Resistance.NotOver", "The game is still being played.");
        }

        await _notifier.ResistanceChangedAsync(sessionId, cancellationToken);

        return ToDto(result.Game!, caller);
    }

    public async Task<ErrorOr<ResistanceGameDto>> SelectTeamAsync(
        Guid sessionId, IReadOnlyList<Guid>? playerIds, GameCaller caller, CancellationToken cancellationToken)
    {
        var access = await _lobby.AccessAsync(sessionId, caller, cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        if (access.Value.Game is null)
        {
            return _lobby.NoGame;
        }

        var result = await _games.SelectTeamAsync(sessionId, caller.PlayerId, playerIds ?? [], cancellationToken);
        switch (result.Status)
        {
            case ResistanceTeamStatus.NoGame:
                return _lobby.NoGame;
            case ResistanceTeamStatus.NotStarted:
                return Error.Conflict("Resistance.NotStarted", "The game has not started yet.");
            case ResistanceTeamStatus.GameOver:
                return GameOverError;
            case ResistanceTeamStatus.NotLeader:
                return Error.Forbidden("Resistance.NotLeader", "Only the mission leader picks the team.");
            case ResistanceTeamStatus.AlreadySubmitted:
                return Error.Conflict("Resistance.TeamSubmitted", "The team was already submitted.");
            case ResistanceTeamStatus.WrongSize:
                return Error.Validation(
                    "Resistance.WrongTeamSize",
                    $"The team needs exactly {result.Game!.Rules.TeamSize(result.Game.Players.Count, result.Game.MissionNumber)} different players.");
            case ResistanceTeamStatus.UnknownPlayer:
                return Error.Validation("Resistance.UnknownPlayer", "The team has a player who is not in the game.");
        }

        await _notifier.ResistanceChangedAsync(sessionId, cancellationToken);

        return ToDto(result.Game!, caller);
    }

    public async Task<ErrorOr<ResistanceGameDto>> VoteAsync(
        Guid sessionId, bool approve, GameCaller caller, CancellationToken cancellationToken)
    {
        var access = await _lobby.AccessAsync(sessionId, caller, cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        if (access.Value.Game is null)
        {
            return _lobby.NoGame;
        }

        var result = await _games.VoteAsync(sessionId, caller.PlayerId, approve, cancellationToken);
        switch (result.Status)
        {
            case ResistanceVoteStatus.NoGame:
                return _lobby.NoGame;
            case ResistanceVoteStatus.NotStarted:
                return Error.Conflict("Resistance.NotStarted", "The game has not started yet.");
            case ResistanceVoteStatus.GameOver:
                return GameOverError;
            case ResistanceVoteStatus.NotPlayer:
                return Error.Forbidden("Resistance.NotPlayer", "Only the players of the game vote.");
            case ResistanceVoteStatus.NotVoting:
                return Error.Conflict("Resistance.NotVoting", "No team is up for a vote.");
        }

        await _notifier.ResistanceChangedAsync(sessionId, cancellationToken);

        return ToDto(result.Game!, caller);
    }

    public async Task<ErrorOr<ResistanceGameDto>> PlayMissionCardAsync(
        Guid sessionId, bool success, GameCaller caller, CancellationToken cancellationToken)
    {
        var access = await _lobby.AccessAsync(sessionId, caller, cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        if (access.Value.Game is null)
        {
            return _lobby.NoGame;
        }

        var result = await _games.PlayMissionCardAsync(sessionId, caller.PlayerId, success, cancellationToken);
        switch (result.Status)
        {
            case ResistanceMissionStatus.NoGame:
                return _lobby.NoGame;
            case ResistanceMissionStatus.NotStarted:
                return Error.Conflict("Resistance.NotStarted", "The game has not started yet.");
            case ResistanceMissionStatus.GameOver:
                return GameOverError;
            case ResistanceMissionStatus.NotPlayer:
                return Error.Forbidden("Resistance.NotPlayer", "Only the players of the game play the mission.");
            case ResistanceMissionStatus.NotPlaying:
                return Error.Conflict("Resistance.NotPlayingMission", "No approved team is playing a mission.");
            case ResistanceMissionStatus.NotOnTeam:
                return Error.Forbidden("Resistance.NotOnTeam", "Only the members of the team play the mission.");
            case ResistanceMissionStatus.AlreadyPlayed:
                return Error.Conflict("Resistance.AlreadyPlayed", "You already played your card.");
            case ResistanceMissionStatus.MustSucceed:
                return Error.Forbidden("Resistance.MustSucceed", "A member of the resistance can only play Success.");
        }

        await _notifier.ResistanceChangedAsync(sessionId, cancellationToken);

        return ToDto(result.Game!, caller);
    }

    public async Task<ErrorOr<ResistanceGameDto>> ContinueAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        var access = await _lobby.AccessAsync(sessionId, caller, cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        if (access.Value.Game is null)
        {
            return _lobby.NoGame;
        }

        var result = await _games.ContinueAsync(sessionId, caller.PlayerId, access.Value.IsManager, cancellationToken);
        switch (result.Status)
        {
            case ResistanceContinueStatus.NoGame:
                return _lobby.NoGame;
            case ResistanceContinueStatus.NotStarted:
                return Error.Conflict("Resistance.NotStarted", "The game has not started yet.");
            case ResistanceContinueStatus.GameOver:
                return GameOverError;
            case ResistanceContinueStatus.NotAllowed:
                return Error.Forbidden("Resistance.NotAllowed", "Only the leader, the host or an admin continues.");
            case ResistanceContinueStatus.NothingToContinue:
                return Error.Conflict("Resistance.NothingToContinue", "There is no finished mission to move on from.");
        }

        await _notifier.ResistanceChangedAsync(sessionId, cancellationToken);

        return ToDto(result.Game!, caller);
    }

    private static Error GameOverError => Error.Conflict("Resistance.GameOver", "The game is over.");

    public Task<ErrorOr<Guid>> JoinAsync(
        Guid sessionId, string? name, GameCaller caller, CancellationToken cancellationToken)
    {
        return _lobby.JoinAsync(sessionId, name, caller, cancellationToken);
    }

    public async Task<ErrorOr<ResistanceGameDto>> KickAsync(
        Guid sessionId, Guid playerId, GameCaller caller, CancellationToken cancellationToken)
    {
        var result = await _lobby.KickAsync(sessionId, playerId, caller, cancellationToken);

        return result.IsError ? result.Errors : ToDto(result.Value, caller);
    }

    public async Task<ErrorOr<ResistanceGameDto>> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid>? playerIds, GameCaller caller, CancellationToken cancellationToken)
    {
        var result = await _lobby.ReorderAsync(sessionId, playerIds, caller, cancellationToken);

        return result.IsError ? result.Errors : ToDto(result.Value, caller);
    }

    public async Task<ErrorOr<Deleted>> EndAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        if (await _lobby.AuthorizeAsync(sessionId, caller, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        if (!await _games.DeleteAsync(sessionId, cancellationToken))
        {
            return _lobby.NoGame;
        }

        await _notifier.ResistanceChangedAsync(sessionId, cancellationToken);

        return Result.Deleted;
    }

    private static ResistanceGameDto ToDto(ResistanceGame game, GameCaller caller)
    {
        var players = game.Players
            .OrderBy(x => x.Order)
            .Select(x => new ResistancePlayerDto(x.PlayerId, x.DisplayName))
            .ToList();

        var you = GameLobby<ResistanceGame, ResistancePlayer>.IsPlayer(game, caller)
            ? game.Players.First(x => x.PlayerId == caller.PlayerId)
            : null;
        var yourRole = game.IsStarted ? you?.Role : null;
        var fellowSpies = yourRole == ResistanceRole.Spy
            ? game.Players
                .Where(x => x.Role == ResistanceRole.Spy && x.PlayerId != you!.PlayerId)
                .OrderBy(x => x.Order)
                .Select(x => x.PlayerId)
                .ToList()
            : null;

        // Hidden while the vote is on; public once all voted (also while the next leader picks after a rejection)
        var revealed = game.Phase != ResistancePhase.Voting && game.Players.All(x => x.Vote is not null);
        var voted = game.Players.Where(x => x.Vote is not null).OrderBy(x => x.Order).ToList();

        return new ResistanceGameDto(
            game.CreatedAt,
            game.IsOpen,
            players,
            you?.PlayerId,
            ResistanceGame.MaxPlayers,
            game.StartedAt,
            game.LeaderPlayerId,
            yourRole is { } role ? (ResistanceRoleDto)role : null,
            fellowSpies,
            game.MissionNumber,
            game.IsStarted ? game.Rules.TeamSize(game.Players.Count, game.MissionNumber) : null,
            game.Players.Where(x => x.IsOnTeam).OrderBy(x => x.Order).Select(x => x.PlayerId).ToList(),
            game.IsStarted ? (ResistancePhaseDto)game.Phase : null,
            game.RejectedTeams,
            game.Winner is { } winner ? (ResistanceRoleDto)winner : null,
            voted.Select(x => x.PlayerId).ToList(),
            you?.Vote,
            revealed ? voted.Select(x => new ResistanceVoteDto(x.PlayerId, x.Vote!.Value)).ToList() : null,
            revealed ? ResistanceRoles.IsApproved(game) : null,
            game.Phase == ResistancePhase.GameOver
                ? game.Players
                    .OrderBy(x => x.Order)
                    .Select(x => new ResistanceRevealedRoleDto(x.PlayerId, (ResistanceRoleDto)x.Role!.Value))
                    .ToList()
                : null,
            new ResistanceRulesDto(
                game.Rules.PlayerCounts
                    .OrderBy(x => x.Key)
                    .Select(x => new ResistanceCountRulesDto(x.Key, x.Value.Spies, x.Value.TeamSizes, x.Value.RequiredFails))
                    .ToList()),
            game.IsStarted ? game.Rules.RequiredFails(game.Players.Count, game.MissionNumber) : null,
            game.Players.Where(x => x.MissionCard is not null).OrderBy(x => x.Order).Select(x => x.PlayerId).ToList(),
            you?.MissionCard,
            game.MissionResults
                .OrderBy(x => x.MissionNumber)
                .Select(x => new ResistanceMissionResultDto(x.MissionNumber, x.Succeeded, x.FailCount))
                .ToList());
    }

    private sealed class ResistanceLobbyNotifier : IGameLobbyNotifier
    {
        private readonly ISessionMusicNotifier _notifier;

        public ResistanceLobbyNotifier(ISessionMusicNotifier notifier)
        {
            _notifier = notifier;
        }

        public Task OpenedAsync(Guid sessionId, CancellationToken cancellationToken) =>
            _notifier.ResistanceOpenedAsync(sessionId, cancellationToken);

        public Task ChangedAsync(Guid sessionId, CancellationToken cancellationToken) =>
            _notifier.ResistanceChangedAsync(sessionId, cancellationToken);
    }
}
