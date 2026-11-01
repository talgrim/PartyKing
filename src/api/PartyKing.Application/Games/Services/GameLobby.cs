using PartyKing.Domain.Repositories;
using ErrorOr;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.System;
using PartyKing.Domain.Entities.Games;

namespace PartyKing.Application.Games.Services;

/// <summary>Tells the session's clients that a game's lobby changed.</summary>
internal interface IGameLobbyNotifier
{
    /// <summary>An admin opened the game; guests are asked whether to join.</summary>
    Task OpenedAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Anything about the game changed; clients fetch it again.</summary>
    Task ChangedAsync(Guid sessionId, CancellationToken cancellationToken);
}

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
    private readonly GameLobbyOptions _options;

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
        _options = options;
    }

    public Error NoGame => Error.NotFound($"{_options.Name}.NotFound", $"The session has no {_options.Name} game.");

    private Error StartedError =>
        Error.Conflict($"{_options.Name}.Started", "The game has started, its players can no longer change.");

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
            return Error.Forbidden($"{_options.Name}.AdminOnly", "Only an admin can open the game to guests.");
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
            return Error.NotFound($"{_options.Name}.SessionNotFound", "The session was not found.");
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
        player.NormalizedName = player.DisplayName.ToUpperInvariant();

        var result = await _games.JoinAsync(sessionId, player, cancellationToken);
        switch (result.Status)
        {
            case LobbyStatus.NoGame:
                return NoGame;
            case LobbyStatus.NotOpen:
                return Error.Conflict($"{_options.Name}.NotOpen", "The game is not open to guests.");
            case LobbyStatus.Started:
                return StartedError;
            case LobbyStatus.NameTaken:
                return Error.Conflict($"{_options.Name}.NameTaken", "Another player already uses this name.");
            case LobbyStatus.Full:
                return Error.Conflict(
                    $"{_options.Name}.Full", $"The game already has {_options.MaxPlayers} players.");
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
        if (result.Status == LobbyStatus.NoGame)
        {
            return NoGame;
        }

        if (result.Status == LobbyStatus.Started)
        {
            return StartedError;
        }

        if (result.Status == LobbyStatus.NoPlayer)
        {
            return Error.NotFound($"{_options.Name}.PlayerNotFound", "The game has no such player.");
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
            return Error.Validation($"{_options.Name}.InvalidOrder", "The player ids are required.");
        }

        var result = await _games.ReorderAsync(sessionId, playerIds, cancellationToken);
        if (result.Status == LobbyStatus.NoGame)
        {
            return NoGame;
        }

        if (result.Status == LobbyStatus.Started)
        {
            return StartedError;
        }

        if (result.Status == LobbyStatus.OrderMismatch)
        {
            return Error.Conflict($"{_options.Name}.OrderMismatch", "The players changed, try again.");
        }

        await _notifier.ChangedAsync(sessionId, cancellationToken);

        return result.Game!;
    }

    private Error? ValidateName(string name)
    {
        if (name.Length == 0)
        {
            return Error.Validation($"{_options.Name}.InvalidName", "The name must not be empty.");
        }

        if (name.Length > LobbyNames.MaxLength)
        {
            return Error.Validation(
                $"{_options.Name}.InvalidName", $"The name must be at most {LobbyNames.MaxLength} characters.");
        }

        // The suffix is how everyone recognizes the host, so a guest can't spell it
        if (name.EndsWith(LobbyPlayer.HostSuffix.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Error.Validation($"{_options.Name}.InvalidName", "The name must not end with (host).");
        }

        return null;
    }
}

internal static class LobbyNames
{
    public const int MaxLength = 30;

    private const string FallbackHostName = "Host";

    /// <summary>The first word of the Spotify display name (all of it when it has no spaces).</summary>
    public static string HostName(string? displayName)
    {
        var first = displayName?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (first is null)
        {
            return FallbackHostName;
        }

        return first.Length > MaxLength ? first[..MaxLength] : first;
    }
}
