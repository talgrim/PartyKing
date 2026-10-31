using ErrorOr;
using PartyKing.Application.Mappings;
using PartyKing.Application.Realtime;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Games.Services.Resistance;

/// <summary>The Resistance game before and around the rounds: lobby, rules, players and the end.</summary>
public interface IResistanceLobbyService
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

    /// <summary>Host or admin: removes a player. NotFound for an unknown game or player.</summary>
    Task<ErrorOr<ResistanceGameDto>> KickAsync(
        Guid sessionId, Guid playerId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>Host or admin: sets the seating order. Conflict when the ids are not exactly the game's players.</summary>
    Task<ErrorOr<ResistanceGameDto>> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid>? playerIds, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>Host or admin: ends the game. NotFound when there is none.</summary>
    Task<ErrorOr<Deleted>> EndAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken);
}

internal class ResistanceLobbyService : IResistanceLobbyService
{
    private readonly ResistanceGameLobby _lobby;
    private readonly ResistanceActionGate _gate;
    private readonly IResistanceGamesRepository _games;
    private readonly IDateTimeProvider _clock;
    private readonly ISessionMusicNotifier _notifier;
    private readonly IResistanceRulesProvider _rulesProvider;

    public ResistanceLobbyService(
        ResistanceGameLobby lobby,
        ResistanceActionGate gate,
        IResistanceGamesRepository games,
        IDateTimeProvider clock,
        ISessionMusicNotifier notifier,
        IResistanceRulesProvider rulesProvider)
    {
        _lobby = lobby;
        _gate = gate;
        _games = games;
        _clock = clock;
        _notifier = notifier;
        _rulesProvider = rulesProvider;
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
                ResistanceGameLobby.IsPlayer(game, caller));
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
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        return _gate.RunAsManagerAsync(
            sessionId,
            caller,
            () => _games.RefreshRulesAsync(sessionId, _rulesProvider.Current(), cancellationToken),
            result => ResistanceErrors.From(
                result.Status, "The rules can only be refreshed before the game is opened."),
            cancellationToken);
    }

    public Task<ErrorOr<ResistanceGameDto>> SetPlotCardsAsync(
        Guid sessionId, bool enabled, GameCaller caller, CancellationToken cancellationToken)
    {
        return _gate.RunAsManagerAsync(
            sessionId,
            caller,
            () => _games.SetPlotCardsAsync(sessionId, enabled, cancellationToken),
            result => ResistanceErrors.From(
                result.Status, "The plot cards can only be switched before the game starts."),
            cancellationToken);
    }

    public async Task<ErrorOr<ResistanceGameDto>> OpenAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        var result = await _lobby.OpenAsync(sessionId, caller, cancellationToken);

        return result.IsError ? result.Errors : ResistanceGameViewBuilder.Build(result.Value, caller);
    }

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
}
