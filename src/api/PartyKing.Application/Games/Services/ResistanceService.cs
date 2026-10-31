using ErrorOr;
using PartyKing.Application.Mappings;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Results;

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

    /// <summary>
    /// Host or admin: switches the plot cards add-on on or off. NotFound without a game, Conflict once the game is
    /// open or started.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> SetPlotCardsAsync(
        Guid sessionId, bool enabled, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// The mission leader hands the drawn plot cards to the other players. Forbidden for anyone else (no manager
    /// override), Conflict when there is nothing to hand out, Validation for a wrong or incomplete deal.
    /// </summary>
    /// <summary>The holder plays an immediate plot card on a player.</summary>
    Task<ErrorOr<ResistanceGameDto>> PlayPlotCardAsync(
        Guid sessionId, int cardId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// The holder of Take Responsibility takes a plot card from another player; Validation when the choice between a
    /// face-up Opinion Maker and a hand card is missing.
    /// </summary>
    Task<ErrorOr<ResistanceGameDto>> TakeResponsibilityAsync(
        Guid sessionId, int cardId, Guid targetPlayerId, bool? takeOpinionMaker, GameCaller caller,
        CancellationToken cancellationToken);

    /// <summary>In the leader window the holder of Strong Leader takes the lead.</summary>
    Task<ErrorOr<ResistanceGameDto>> TakeLeadAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>The holder of Keeping a Close Eye looks at a team member's mission card after the mission.</summary>
    Task<ErrorOr<ResistanceGameDto>> PeekMissionCardAsync(
        Guid sessionId, int cardId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>In the confidence window the holder of No Confidence cancels the approved team.</summary>
    Task<ErrorOr<ResistanceGameDto>> PlayNoConfidenceAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>A player confirms in the leader or confidence window that they play no card; the last confirmation ends it.</summary>
    Task<ErrorOr<ResistanceGameDto>> PassLeaderWindowAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>The player who saw the identity card confirms it; the last confirmation moves on to the team.</summary>
    Task<ErrorOr<ResistanceGameDto>> ConfirmPlotCardAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken);

    Task<ErrorOr<ResistanceGameDto>> DealPlotCardsAsync(
        Guid sessionId, IReadOnlyList<ResistancePlotCardAssignmentDto>? assignments, GameCaller caller,
        CancellationToken cancellationToken);

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
    private readonly ResistanceActionGate _gate;

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
            new GameLobbyOptions(ResistanceErrors.GameName, ResistanceGame.MaxPlayers));
        _gate = new ResistanceActionGate(_lobby, notifier);
    }

    public async Task<ErrorOr<ResistanceGameDto>> GetAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        // A caller with neither a host profile nor an admin cookie can't be a manager, so skip the session read
        var isManager = false;
        if (caller.ProfileId is not null || caller.IsAdmin)
        {
            var authorization = await _lobby.AuthorizeAsync(sessionId, caller, cancellationToken);
            if (authorization.IsError && authorization.FirstError.Type != ErrorType.Forbidden)
            {
                return authorization.Errors;
            }

            isManager = !authorization.IsError;
        }

        var game = await _games.GetAsync(sessionId, cancellationToken);

        return game is null || (!isManager && !game.IsOpen)
            ? ResistanceErrors.NoGame
            : ResistanceGameViewBuilder.Build(game, caller);
    }

    public async Task<ErrorOr<ResistanceLobbyDto>> GetLobbyAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        var game = await _games.GetAsync(sessionId, cancellationToken);

        return game is null
            ? ResistanceErrors.NoGame
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
            return ResistanceErrors.AlreadyCreated;
        }

        await _notifier.ResistanceChangedAsync(sessionId, cancellationToken);

        return ResistanceGameViewBuilder.Build(game, caller);
    }

    public Task<ErrorOr<ResistanceGameDto>> RefreshRulesAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken) =>
        _gate.RunAsManagerAsync(
            sessionId,
            caller,
            () => _games.RefreshRulesAsync(sessionId, _rulesProvider.Current(), cancellationToken),
            result => ResistanceErrors.From(
                result.Status, "The rules can only be refreshed before the game is opened."),
            cancellationToken);

    public Task<ErrorOr<ResistanceGameDto>> SetPlotCardsAsync(
        Guid sessionId, bool enabled, GameCaller caller, CancellationToken cancellationToken) =>
        _gate.RunAsManagerAsync(
            sessionId,
            caller,
            () => _games.SetPlotCardsAsync(sessionId, enabled, cancellationToken),
            result => ResistanceErrors.From(
                result.Status, "The plot cards can only be switched before the game starts."),
            cancellationToken);

    public async Task<ErrorOr<ResistanceGameDto>> OpenAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        var result = await _lobby.OpenAsync(sessionId, caller, cancellationToken);

        return result.IsError ? result.Errors : ResistanceGameViewBuilder.Build(result.Value, caller);
    }

    public Task<ErrorOr<ResistanceGameDto>> StartAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken) =>
        _gate.RunAsManagerAsync(
            sessionId,
            caller,
            () => _games.StartAsync(sessionId, _clock.UtcNow, cancellationToken),
            result => ResistanceErrors.From(result.Status),
            cancellationToken);

    public Task<ErrorOr<ResistanceGameDto>> RestartAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken) =>
        _gate.RunAsManagerAsync(
            sessionId,
            caller,
            () => _games.RestartAsync(sessionId, cancellationToken),
            result => ResistanceErrors.From(result.Status),
            cancellationToken);

    public Task<ErrorOr<ResistanceGameDto>> SelectTeamAsync(
        Guid sessionId, IReadOnlyList<Guid>? playerIds, GameCaller caller, CancellationToken cancellationToken) =>
        _gate.RunAsPlayerAsync(
            sessionId,
            caller,
            _ => _games.SelectTeamAsync(sessionId, caller.PlayerId, playerIds ?? [], cancellationToken),
            result => ResistanceErrors.From(result.Status, result.Game),
            cancellationToken);

    public Task<ErrorOr<ResistanceGameDto>> DealPlotCardsAsync(
        Guid sessionId, IReadOnlyList<ResistancePlotCardAssignmentDto>? assignments, GameCaller caller,
        CancellationToken cancellationToken) =>
        _gate.RunAsPlayerAsync(
            sessionId,
            caller,
            _ => _games.DealPlotCardsAsync(
                sessionId,
                caller.PlayerId,
                (assignments ?? []).Select(x => new PlotCardAssignment(x.CardId, x.PlayerId)).ToList(),
                cancellationToken),
            result => ResistanceErrors.From(result.Status),
            cancellationToken);

    public Task<ErrorOr<ResistanceGameDto>> PlayPlotCardAsync(
        Guid sessionId, int cardId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken) =>
        RunPlotCardAsync(
            sessionId,
            caller,
            () => _games.PlayPlotCardAsync(sessionId, caller.PlayerId, cardId, targetPlayerId, cancellationToken),
            cancellationToken);

    public Task<ErrorOr<ResistanceGameDto>> ConfirmPlotCardAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken) =>
        RunPlotCardAsync(
            sessionId,
            caller,
            () => _games.ConfirmPlotCardAsync(sessionId, caller.PlayerId, cardId, cancellationToken),
            cancellationToken);

    public Task<ErrorOr<ResistanceGameDto>> TakeResponsibilityAsync(
        Guid sessionId, int cardId, Guid targetPlayerId, bool? takeOpinionMaker, GameCaller caller,
        CancellationToken cancellationToken) =>
        RunPlotCardAsync(
            sessionId,
            caller,
            () => _games.TakeResponsibilityAsync(
                sessionId, caller.PlayerId, cardId, targetPlayerId, takeOpinionMaker, cancellationToken),
            cancellationToken);

    public Task<ErrorOr<ResistanceGameDto>> TakeLeadAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken) =>
        RunPlotCardAsync(
            sessionId,
            caller,
            () => _games.TakeLeadAsync(sessionId, caller.PlayerId, cardId, cancellationToken),
            cancellationToken);

    public Task<ErrorOr<ResistanceGameDto>> PeekMissionCardAsync(
        Guid sessionId, int cardId, Guid targetPlayerId, GameCaller caller, CancellationToken cancellationToken) =>
        RunPlotCardAsync(
            sessionId,
            caller,
            () => _games.PeekMissionCardAsync(sessionId, caller.PlayerId, cardId, targetPlayerId, cancellationToken),
            cancellationToken);

    public Task<ErrorOr<ResistanceGameDto>> PlayNoConfidenceAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken) =>
        RunPlotCardAsync(
            sessionId,
            caller,
            () => _games.PlayNoConfidenceAsync(sessionId, caller.PlayerId, cardId, cancellationToken),
            cancellationToken);

    public Task<ErrorOr<ResistanceGameDto>> PassLeaderWindowAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken) =>
        RunPlotCardAsync(
            sessionId,
            caller,
            () => _games.PassLeaderWindowAsync(sessionId, caller.PlayerId, cancellationToken),
            cancellationToken);

    private Task<ErrorOr<ResistanceGameDto>> RunPlotCardAsync(
        Guid sessionId,
        GameCaller caller,
        Func<Task<ResistancePlayCardResult>> call,
        CancellationToken cancellationToken) =>
        _gate.RunAsPlayerAsync(
            sessionId, caller, _ => call(), result => ResistanceErrors.From(result.Status), cancellationToken);

    public Task<ErrorOr<ResistanceGameDto>> VoteAsync(
        Guid sessionId, bool approve, GameCaller caller, CancellationToken cancellationToken) =>
        _gate.RunAsPlayerAsync(
            sessionId,
            caller,
            _ => _games.VoteAsync(sessionId, caller.PlayerId, approve, cancellationToken),
            result => ResistanceErrors.From(result.Status),
            cancellationToken);

    public Task<ErrorOr<ResistanceGameDto>> PlayMissionCardAsync(
        Guid sessionId, bool success, GameCaller caller, CancellationToken cancellationToken) =>
        _gate.RunAsPlayerAsync(
            sessionId,
            caller,
            _ => _games.PlayMissionCardAsync(sessionId, caller.PlayerId, success, cancellationToken),
            result => ResistanceErrors.From(result.Status),
            cancellationToken);

    public Task<ErrorOr<ResistanceGameDto>> ContinueAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken) =>
        _gate.RunAsPlayerAsync(
            sessionId,
            caller,
            access => _games.ContinueAsync(sessionId, caller.PlayerId, access.IsManager, cancellationToken),
            result => ResistanceErrors.From(result.Status),
            cancellationToken);

    public Task<ErrorOr<Guid>> JoinAsync(
        Guid sessionId, string? name, GameCaller caller, CancellationToken cancellationToken)
    {
        return _lobby.JoinAsync(sessionId, name, caller, cancellationToken);
    }

    public async Task<ErrorOr<ResistanceGameDto>> KickAsync(
        Guid sessionId, Guid playerId, GameCaller caller, CancellationToken cancellationToken)
    {
        var result = await _lobby.KickAsync(sessionId, playerId, caller, cancellationToken);

        return result.IsError ? result.Errors : ResistanceGameViewBuilder.Build(result.Value, caller);
    }

    public async Task<ErrorOr<ResistanceGameDto>> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid>? playerIds, GameCaller caller, CancellationToken cancellationToken)
    {
        var result = await _lobby.ReorderAsync(sessionId, playerIds, caller, cancellationToken);

        return result.IsError ? result.Errors : ResistanceGameViewBuilder.Build(result.Value, caller);
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
            return ResistanceErrors.NoGame;
        }

        await _notifier.ResistanceChangedAsync(sessionId, cancellationToken);

        return Result.Deleted;
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
