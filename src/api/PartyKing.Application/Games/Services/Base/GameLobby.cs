using PartyKing.Domain.Repositories;
using ErrorOr;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.System;
using PartyKing.Domain.Entities.Games;

namespace PartyKing.Application.Games.Services.Base;

/// <param name="Name">How the game is called, in errors ("Ringo"); also the prefix of their codes.</param>
/// <param name="MaxPlayers">How many players may join.</param>
internal record GameLobbyOptions(string Name, int MaxPlayers = int.MaxValue);

/// <summary>
/// The rules every game with a lobby shares: who may manage it, an admin opening it, joining by name (the host under
/// their profile name, an admin under a chosen name or "admin"), kicking and ordering the players.
/// </summary>
internal class GameLobby<TGame, TPlayer>
    where TGame : LobbyGame<TPlayer>
    where TPlayer : LobbyPlayer, new()
{
    public const string AdminName = "admin";

    private readonly ISessionAuthorizer _sessionAuthorizer;
    private readonly ISessionsRepository _sessions;
    private readonly ILobbyGamesRepository<TGame, TPlayer> _games;
    private readonly IDateTimeProvider _clock;
    private readonly IGameLobbyNotifier _notifier;
    private readonly LobbyErrors _errors;

    public GameLobby(
        ISessionAuthorizer sessionAuthorizer,
        ISessionsRepository sessions,
        ILobbyGamesRepository<TGame, TPlayer> games,
        IDateTimeProvider clock,
        IGameLobbyNotifier notifier,
        GameLobbyOptions options)
    {
        _sessionAuthorizer = sessionAuthorizer;
        _sessions = sessions;
        _games = games;
        _clock = clock;
        _notifier = notifier;
        _errors = new LobbyErrors(options);
    }

    public Error NoGame => _errors.NoGame;

    /// <summary>Host or admin; Forbidden or NotFound otherwise.</summary>
    public Task<ErrorOr<Success>> AuthorizeAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        return _sessionAuthorizer.AuthorizeManagerAsync(sessionId, caller, cancellationToken);
    }

    /// <summary>The caller's own player in the game, or null when the caller only watches.</summary>
    public static TPlayer? PlayerOf(TGame game, GameCaller caller)
    {
        return caller.PlayerId is { } id ? game.Players.FirstOrDefault(x => x.PlayerId == id) : null;
    }

    public static bool IsPlayer(TGame game, GameCaller caller)
    {
        return PlayerOf(game, caller) is not null;
    }

    public readonly record struct Access(bool IsManager, TGame? Game);

    /// <summary>
    /// The host and admins always get in. Anyone else needs to be a player of the game, and is Forbidden whether or
    /// not a game exists, so a guest can't tell.
    /// </summary>
    public async Task<ErrorOr<Access>> AccessAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        var authorization = await AuthorizeAsync(sessionId, caller, cancellationToken);
        if (authorization.IsError && authorization.FirstError.Type != ErrorType.Forbidden)
        {
            return authorization.Errors;
        }

        var game = await _games.GetAsync(sessionId, cancellationToken);
        if (authorization.IsError && (game is null || !IsPlayer(game, caller)))
        {
            return authorization.Errors;
        }

        return new Access(!authorization.IsError, game);
    }

    /// <summary>Admin only: opens the game to guests (idempotent). NotFound without a game, Forbidden for the host.</summary>
    public async Task<ErrorOr<TGame>> OpenAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        if (!caller.IsAdmin)
        {
            return _errors.AdminOnly;
        }

        var existing = await _games.GetAsync(sessionId, cancellationToken);
        if (existing is null)
        {
            return NoGame;
        }

        if (existing.IsOpen)
        {
            return existing;
        }

        var game = await _games.OpenAsync(sessionId, cancellationToken);
        if (game is null)
        {
            return NoGame;
        }

        await _notifier.OpenedAsync(sessionId, cancellationToken);
        await _notifier.ChangedAsync(sessionId, cancellationToken);

        return game;
    }

    /// <summary>
    /// Joins an open game and returns the new player's id (the caller's own one when they already joined). The
    /// session host joins under the first name of their Spotify profile and ignores <paramref name="name"/>; an
    /// admin without a name joins as "admin". Validation error for an empty or over-long name, Conflict while the
    /// game is closed, full or the name is taken, NotFound without a game.
    /// </summary>
    public async Task<ErrorOr<Guid>> JoinAsync(
        Guid sessionId, string? name, GameCaller caller, CancellationToken cancellationToken)
    {
        var session = await _sessions.GetAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return _errors.SessionNotFound;
        }

        var game = await _games.GetAsync(sessionId, cancellationToken);
        if (game is null)
        {
            return NoGame;
        }

        if (caller.PlayerId is { } existing && game.Players.Any(x => x.PlayerId == existing))
        {
            return existing;
        }

        var isHost = SessionRoles.IsHost(session, caller);
        var typed = name?.Trim() ?? string.Empty;
        var playerName = isHost
            ? LobbyNames.HostName(session.HostProfile?.DisplayName)
            : caller.IsAdmin && typed.Length == 0 ? AdminName : typed;
        if (!isHost && ValidateName(playerName) is { } invalid)
        {
            return invalid;
        }

        var player = new TPlayer
        {
            PlayerId = Guid.NewGuid(),
            Name = playerName,
            ProfileId = isHost ? caller.ProfileId : null,
            JoinedAt = _clock.UtcNow,
        };

        var result = await _games.JoinAsync(sessionId, player, cancellationToken);
        if (_errors.From(result.Status) is { } error)
        {
            return error;
        }

        await _notifier.ChangedAsync(sessionId, cancellationToken);

        return player.PlayerId;
    }

    /// <summary>Host or admin: removes a player. NotFound for an unknown game or player.</summary>
    public async Task<ErrorOr<TGame>> KickAsync(
        Guid sessionId, Guid playerId, GameCaller caller, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(sessionId, caller, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        var result = await _games.KickAsync(sessionId, playerId, cancellationToken);
        if (_errors.From(result.Status) is { } error)
        {
            return error;
        }

        await _notifier.ChangedAsync(sessionId, cancellationToken);

        return result.Game!;
    }

    /// <summary>Host or admin: sets the player order. Conflict when the ids are not exactly the game's players.</summary>
    public async Task<ErrorOr<TGame>> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid>? playerIds, GameCaller caller, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(sessionId, caller, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        if (playerIds is null)
        {
            return _errors.InvalidOrder;
        }

        var result = await _games.ReorderAsync(sessionId, playerIds, cancellationToken);
        if (_errors.From(result.Status) is { } error)
        {
            return error;
        }

        await _notifier.ChangedAsync(sessionId, cancellationToken);

        return result.Game!;
    }

    private Error? ValidateName(string name)
    {
        return LobbyNames.Validate(name) is { } failure ? _errors.From(failure) : null;
    }
}
