using ErrorOr;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Games;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;


namespace PartyKing.Application.Games.Services;

/// <summary>Who calls: the host's Spotify profile id, whether an admin, and the player id of the caller's device.</summary>
public record RingoCaller(string? ProfileId, bool IsAdmin, Guid? PlayerId = null);

public interface IRingoService
{
    /// <summary>The session's game. NotFound when there is none; Forbidden unless host, admin or a player of it.</summary>
    Task<ErrorOr<RingoGameDto>> GetAsync(Guid sessionId, RingoCaller caller, CancellationToken cancellationToken);

    /// <summary>What a guest may know: whether the game is open and whether the caller joined. NotFound without a game.</summary>
    Task<ErrorOr<RingoLobbyDto>> GetLobbyAsync(Guid sessionId, RingoCaller caller, CancellationToken cancellationToken);

    /// <summary>Starts a game with a random layout. Conflict when the session already has one.</summary>
    Task<ErrorOr<RingoGameDto>> StartAsync(Guid sessionId, RingoCaller caller, CancellationToken cancellationToken);

    /// <summary>Admin only: opens the game to guests (idempotent). NotFound without a game, Forbidden for the host.</summary>
    Task<ErrorOr<RingoGameDto>> OpenAsync(Guid sessionId, RingoCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// Joins an open game and returns the new player's id (the caller's own one when they already joined). The session
    /// host joins under the first name of their Spotify profile and ignores <paramref name="name"/>. Validation error
    /// for an empty or over-long name, Conflict while the game is closed or the name is taken, NotFound without a game.
    /// </summary>
    Task<ErrorOr<Guid>> JoinAsync(Guid sessionId, string? name, RingoCaller caller, CancellationToken cancellationToken);

    /// <summary>Host or admin: removes a player. NotFound for an unknown game or player.</summary>
    Task<ErrorOr<RingoGameDto>> KickAsync(Guid sessionId, Guid playerId, RingoCaller caller, CancellationToken cancellationToken);

    /// <summary>Host or admin: sets the turn order. Conflict when the ids are not exactly the game's players.</summary>
    Task<ErrorOr<RingoGameDto>> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid>? playerIds, RingoCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// Flips a card and makes it the pending one (idempotent for a flipped card). Host and admins may flip at any
    /// time, a player only on their turn (Conflict otherwise). Validation error for an id outside 0-51, NotFound
    /// without a game, Conflict while another card is pending or after the fourth king.
    /// </summary>
    Task<ErrorOr<RingoGameDto>> FlipAsync(Guid sessionId, int cardId, RingoCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// Clears the pending card (idempotent) and passes the turn on. Host, admins and the player whose turn it is.
    /// NotFound without a game.
    /// </summary>
    Task<ErrorOr<RingoGameDto>> ContinueAsync(Guid sessionId, RingoCaller caller, CancellationToken cancellationToken);

    /// <summary>Ends the game. NotFound when there is none.</summary>
    Task<ErrorOr<Deleted>> EndAsync(Guid sessionId, RingoCaller caller, CancellationToken cancellationToken);
}

internal class RingoService : IRingoService
{
    public const int CardCount = 52;
    public const int MaxNameLength = 30;

    private const string FallbackHostName = "Host";

    private readonly ISessionService _sessionService;
    private readonly ISessionsRepository _sessions;
    private readonly IRingoGamesRepository _games;
    private readonly IDateTimeProvider _clock;
    private readonly ISessionMusicNotifier _notifier;

    public RingoService(
        ISessionService sessionService,
        ISessionsRepository sessions,
        IRingoGamesRepository games,
        IDateTimeProvider clock,
        ISessionMusicNotifier notifier)
    {
        _sessionService = sessionService;
        _sessions = sessions;
        _games = games;
        _clock = clock;
        _notifier = notifier;
    }

    public async Task<ErrorOr<RingoGameDto>> GetAsync(
        Guid sessionId, RingoCaller caller, CancellationToken cancellationToken)
    {
        var access = await AccessAsync(sessionId, caller, cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        return access.Value.Game is null ? NoGame : ToDto(access.Value.Game, caller);
    }

    public async Task<ErrorOr<RingoLobbyDto>> GetLobbyAsync(
        Guid sessionId, RingoCaller caller, CancellationToken cancellationToken)
    {
        var game = await _games.GetAsync(sessionId, cancellationToken);

        return game is null ? NoGame : new RingoLobbyDto(game.Seed, game.IsOpen, IsPlayer(game, caller));
    }

    public async Task<ErrorOr<RingoGameDto>> StartAsync(
        Guid sessionId, RingoCaller caller, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(sessionId, caller, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        var game = new RingoGame
        {
            SessionId = sessionId,
            Seed = Random.Shared.Next(),
            CreatedAt = _clock.UtcNow,
        };

        if (!await _games.TryCreateAsync(game, cancellationToken))
        {
            return Error.Conflict("Ringo.AlreadyStarted", "The session already has a Ringo game.");
        }

        await _notifier.RingoChangedAsync(sessionId, cancellationToken);

        return ToDto(game, caller);
    }

    public async Task<ErrorOr<RingoGameDto>> OpenAsync(
        Guid sessionId, RingoCaller caller, CancellationToken cancellationToken)
    {
        if (!caller.IsAdmin)
        {
            return Error.Forbidden("Ringo.AdminOnly", "Only an admin can open the game to guests.");
        }

        var existing = await _games.GetAsync(sessionId, cancellationToken);
        if (existing is null)
        {
            return NoGame;
        }

        if (existing.IsOpen)
        {
            return ToDto(existing, caller);
        }

        var game = await _games.OpenAsync(sessionId, cancellationToken);
        if (game is null)
        {
            return NoGame;
        }

        await _notifier.RingoOpenedAsync(sessionId, cancellationToken);
        await _notifier.RingoChangedAsync(sessionId, cancellationToken);

        return ToDto(game, caller);
    }

    public async Task<ErrorOr<Guid>> JoinAsync(
        Guid sessionId, string? name, RingoCaller caller, CancellationToken cancellationToken)
    {
        var session = await _sessions.GetAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return Error.NotFound("Ringo.SessionNotFound", "The session was not found.");
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

        var isHost = caller.ProfileId is not null && session.HostProfileId == caller.ProfileId;
        var playerName = isHost ? HostName(session.HostProfile?.DisplayName) : name?.Trim() ?? string.Empty;
        if (!isHost && ValidateName(playerName) is { } invalid)
        {
            return invalid;
        }

        var player = new RingoPlayer
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
            case RingoPlayersStatus.NoGame:
                return NoGame;
            case RingoPlayersStatus.NotOpen:
                return Error.Conflict("Ringo.NotOpen", "The game is not open to guests.");
            case RingoPlayersStatus.NameTaken:
                return Error.Conflict("Ringo.NameTaken", "Another player already uses this name.");
        }

        await _notifier.RingoChangedAsync(sessionId, cancellationToken);

        return player.PlayerId;
    }

    public async Task<ErrorOr<RingoGameDto>> KickAsync(
        Guid sessionId, Guid playerId, RingoCaller caller, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(sessionId, caller, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        var result = await _games.KickAsync(sessionId, playerId, cancellationToken);
        if (result.Status == RingoPlayersStatus.NoGame)
        {
            return NoGame;
        }

        if (result.Status == RingoPlayersStatus.NoPlayer)
        {
            return Error.NotFound("Ringo.PlayerNotFound", "The game has no such player.");
        }

        await _notifier.RingoChangedAsync(sessionId, cancellationToken);

        return ToDto(result.Game!, caller);
    }

    public async Task<ErrorOr<RingoGameDto>> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid>? playerIds, RingoCaller caller, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(sessionId, caller, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        if (playerIds is null)
        {
            return Error.Validation("Ringo.InvalidOrder", "The player ids are required.");
        }

        var result = await _games.ReorderAsync(sessionId, playerIds, cancellationToken);
        if (result.Status == RingoPlayersStatus.NoGame)
        {
            return NoGame;
        }

        if (result.Status == RingoPlayersStatus.OrderMismatch)
        {
            return Error.Conflict("Ringo.OrderMismatch", "The players changed, try again.");
        }

        await _notifier.RingoChangedAsync(sessionId, cancellationToken);

        return ToDto(result.Game!, caller);
    }

    public async Task<ErrorOr<RingoGameDto>> FlipAsync(
        Guid sessionId, int cardId, RingoCaller caller, CancellationToken cancellationToken)
    {
        var access = await AccessAsync(sessionId, caller, cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        if (cardId is < 0 or >= CardCount)
        {
            return Error.Validation("Ringo.InvalidCard", $"The card id must be between 0 and {CardCount - 1}.");
        }

        // Without players there is no name to record, so the role stands in
        var flippedBy = caller.IsAdmin ? "admin" : "guest";
        var turnOf = access.Value.IsManager ? null : caller.PlayerId;
        var result = await _games.FlipAsync(sessionId, cardId, flippedBy, turnOf, _clock.UtcNow, cancellationToken);

        switch (result.Status)
        {
            case RingoFlipStatus.NoGame:
                return NoGame;
            case RingoFlipStatus.NotYourTurn:
                return Error.Conflict("Ringo.NotYourTurn", "It is another player's turn.");
            case RingoFlipStatus.GameOver:
                return Error.Conflict("Ringo.GameOver", "The fourth king ended the game.");
            case RingoFlipStatus.CardPending:
                return Error.Conflict("Ringo.CardPending", "Continue past the flipped card before flipping another.");
            case RingoFlipStatus.Flipped:
                await _notifier.RingoChangedAsync(sessionId, cancellationToken);
                break;
        }

        return ToDto(result.Game!, caller);
    }

    public async Task<ErrorOr<RingoGameDto>> ContinueAsync(
        Guid sessionId, RingoCaller caller, CancellationToken cancellationToken)
    {
        var access = await AccessAsync(sessionId, caller, cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        var turnOf = access.Value.IsManager ? null : caller.PlayerId;
        var result = await _games.ContinueAsync(sessionId, turnOf, cancellationToken);
        if (result.Status == RingoContinueStatus.NoGame)
        {
            return NoGame;
        }

        if (result.Status == RingoContinueStatus.NotYourTurn)
        {
            return Error.Conflict("Ringo.NotYourTurn", "It is another player's turn.");
        }

        await _notifier.RingoChangedAsync(sessionId, cancellationToken);

        return ToDto(result.Game!, caller);
    }

    public async Task<ErrorOr<Deleted>> EndAsync(
        Guid sessionId, RingoCaller caller, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(sessionId, caller, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        if (!await _games.DeleteAsync(sessionId, cancellationToken))
        {
            return NoGame;
        }

        await _notifier.RingoChangedAsync(sessionId, cancellationToken);

        return Result.Deleted;
    }

    /// <summary>The first word of the Spotify display name (all of it when it has no spaces).</summary>
    public static string HostName(string? displayName)
    {
        var first = displayName?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (first is null)
        {
            return FallbackHostName;
        }

        return first.Length > MaxNameLength ? first[..MaxNameLength] : first;
    }

    private static Error NoGame => Error.NotFound("Ringo.NotFound", "The session has no Ringo game.");

    private static Error? ValidateName(string name)
    {
        if (name.Length == 0)
        {
            return Error.Validation("Ringo.InvalidName", "The name must not be empty.");
        }

        if (name.Length > MaxNameLength)
        {
            return Error.Validation("Ringo.InvalidName", $"The name must be at most {MaxNameLength} characters.");
        }

        // The suffix is how everyone recognizes the host, so a guest can't spell it
        if (name.EndsWith(RingoPlayer.HostSuffix.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Error.Validation("Ringo.InvalidName", "The name must not end with (host).");
        }

        return null;
    }

    private static bool IsPlayer(RingoGame game, RingoCaller caller)
    {
        return caller.PlayerId is { } id && game.Players.Any(x => x.PlayerId == id);
    }

    private Task<ErrorOr<Success>> AuthorizeAsync(
        Guid sessionId, RingoCaller caller, CancellationToken cancellationToken)
    {
        return _sessionService.AuthorizeManagerAsync(sessionId, caller.ProfileId, caller.IsAdmin, cancellationToken);
    }

    private readonly record struct Access(bool IsManager, RingoGame? Game);

    /// <summary>
    /// The host and admins always get in. Anyone else needs to be a player of the game, and is Forbidden whether or
    /// not a game exists, so a guest can't tell.
    /// </summary>
    private async Task<ErrorOr<Access>> AccessAsync(
        Guid sessionId, RingoCaller caller, CancellationToken cancellationToken)
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

    private static RingoGameDto ToDto(RingoGame game, RingoCaller caller)
    {
        var flips = game.Flips
            .OrderBy(x => x.Order)
            .Select(x => new RingoFlipDto(x.CardId, x.FlippedAt, x.FlippedBy))
            .ToList();
        var players = game.Players
            .OrderBy(x => x.Order)
            .Select(x => new RingoPlayerDto(x.PlayerId, x.DisplayName))
            .ToList();

        return new RingoGameDto(
            game.Seed,
            flips,
            game.PendingCardId,
            game.IsOpen,
            players,
            RingoTurns.Current(game)?.PlayerId,
            IsPlayer(game, caller) ? caller.PlayerId : null);
    }
}
