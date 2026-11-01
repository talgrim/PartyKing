using PartyKing.Domain.Repositories;
using ErrorOr;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Application.Realtime;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Games.Ringo;
using PartyKing.Domain.Entities.Games.Ringo;


namespace PartyKing.Application.Games.Services.Ringo;

public interface IRingoService
{
    /// <summary>The session's game. NotFound when there is none; Forbidden unless host, admin or a player of it.</summary>
    Task<ErrorOr<RingoGameDto>> GetAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>What a guest may know: whether the game is open and whether the caller joined. NotFound without a game.</summary>
    Task<ErrorOr<RingoLobbyDto>> GetLobbyAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>Starts a game with a random layout. Conflict when the session already has one.</summary>
    Task<ErrorOr<RingoGameDto>> StartAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>Admin only: opens the game to guests (idempotent). NotFound without a game, Forbidden for the host.</summary>
    Task<ErrorOr<RingoGameDto>> OpenAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// Joins an open game and returns the new player's id (the caller's own one when they already joined). The session
    /// host joins under the first name of their Spotify profile and ignores <paramref name="name"/>; an admin without
    /// a name joins as "admin". Validation error for an empty or over-long name, Conflict while the game is closed or
    /// the name is taken, NotFound without a game.
    /// </summary>
    Task<ErrorOr<Guid>> JoinAsync(Guid sessionId, string? name, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>Host or admin: removes a player. NotFound for an unknown game or player.</summary>
    Task<ErrorOr<RingoGameDto>> KickAsync(Guid sessionId, Guid playerId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>Host or admin: sets the turn order. Conflict when the ids are not exactly the game's players.</summary>
    Task<ErrorOr<RingoGameDto>> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid>? playerIds, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// Flips a card and makes it the pending one (idempotent for a flipped card). Host and admins may flip at any
    /// time, a player only on their turn (Conflict otherwise). Validation error for an id outside 0-51, NotFound
    /// without a game, Conflict while another card is pending or after the fourth king.
    /// </summary>
    Task<ErrorOr<RingoGameDto>> FlipAsync(Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// Clears the pending card (idempotent) and passes the turn on. Host, admins and the player whose turn it is.
    /// NotFound without a game.
    /// </summary>
    Task<ErrorOr<RingoGameDto>> ContinueAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken);

    /// <summary>Ends the game. NotFound when there is none.</summary>
    Task<ErrorOr<Deleted>> EndAsync(Guid sessionId, GameCaller caller, CancellationToken cancellationToken);
}

internal class RingoService : IRingoService
{
    public const int CardCount = 52;

    private readonly IRingoGamesRepository _games;
    private readonly IDateTimeProvider _clock;
    private readonly IRingoNotifier _notifier;
    private readonly RingoGameLobby _lobby;

    public RingoService(
        RingoGameLobby lobby,
        IRingoGamesRepository games,
        IDateTimeProvider clock,
        IRingoNotifier notifier)
    {
        _lobby = lobby;
        _games = games;
        _clock = clock;
        _notifier = notifier;
    }

    public async Task<ErrorOr<RingoGameDto>> GetAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        var access = await _lobby.AccessAsync(sessionId, caller, cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        return access.Value.Game is null ? _lobby.NoGame : ToDto(access.Value.Game, caller);
    }

    public async Task<ErrorOr<RingoLobbyDto>> GetLobbyAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        var game = await _games.GetAsync(sessionId, cancellationToken);

        return game is null
            ? _lobby.NoGame
            : new RingoLobbyDto(game.Seed, game.IsOpen, RingoGameLobby.IsPlayer(game, caller));
    }

    public async Task<ErrorOr<RingoGameDto>> StartAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        if (await _lobby.AuthorizeAsync(sessionId, caller, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        var game = RingoGame.Start(sessionId, Random.Shared.Next(), _clock.UtcNow);

        if (!await _games.TryCreateAsync(game, cancellationToken))
        {
            return Error.Conflict("Ringo.AlreadyStarted", "The session already has a Ringo game.");
        }

        await _notifier.ChangedAsync(sessionId, cancellationToken);

        return ToDto(game, caller);
    }

    public async Task<ErrorOr<RingoGameDto>> OpenAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        var result = await _lobby.OpenAsync(sessionId, caller, cancellationToken);

        return result.IsError ? result.Errors : ToDto(result.Value, caller);
    }

    public Task<ErrorOr<Guid>> JoinAsync(
        Guid sessionId, string? name, GameCaller caller, CancellationToken cancellationToken)
    {
        return _lobby.JoinAsync(sessionId, name, caller, cancellationToken);
    }

    public async Task<ErrorOr<RingoGameDto>> KickAsync(
        Guid sessionId, Guid playerId, GameCaller caller, CancellationToken cancellationToken)
    {
        var result = await _lobby.KickAsync(sessionId, playerId, caller, cancellationToken);

        return result.IsError ? result.Errors : ToDto(result.Value, caller);
    }

    public async Task<ErrorOr<RingoGameDto>> ReorderAsync(
        Guid sessionId, IReadOnlyList<Guid>? playerIds, GameCaller caller, CancellationToken cancellationToken)
    {
        var result = await _lobby.ReorderAsync(sessionId, playerIds, caller, cancellationToken);

        return result.IsError ? result.Errors : ToDto(result.Value, caller);
    }

    public async Task<ErrorOr<RingoGameDto>> FlipAsync(
        Guid sessionId, int cardId, GameCaller caller, CancellationToken cancellationToken)
    {
        var access = await _lobby.AccessAsync(sessionId, caller, cancellationToken);
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
        var outcome = await _games.UpdateAsync(
            sessionId,
            nameof(FlipAsync),
            RingoOutcome.NoGame,
            game => game.Flip(cardId, flippedBy, turnOf, _clock.UtcNow),
            cancellationToken);
        if (RingoErrors.From(outcome) is { } error)
        {
            return error;
        }

        // A card that was flipped already changes nothing, so there is nothing to tell the clients
        if (outcome.Changed)
        {
            await _notifier.ChangedAsync(sessionId, cancellationToken);
        }

        return ToDto(outcome.Game!, caller);
    }

    public async Task<ErrorOr<RingoGameDto>> ContinueAsync(
        Guid sessionId, GameCaller caller, CancellationToken cancellationToken)
    {
        var access = await _lobby.AccessAsync(sessionId, caller, cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        var turnOf = access.Value.IsManager ? null : caller.PlayerId;
        var outcome = await _games.UpdateAsync(
            sessionId,
            nameof(ContinueAsync),
            RingoOutcome.NoGame,
            game => game.Continue(turnOf),
            cancellationToken);
        if (RingoErrors.From(outcome) is { } error)
        {
            return error;
        }

        // Continuing with no card pending changes nothing, so there is nothing to tell the clients
        if (outcome.Changed)
        {
            await _notifier.ChangedAsync(sessionId, cancellationToken);
        }

        return ToDto(outcome.Game!, caller);
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

        await _notifier.ChangedAsync(sessionId, cancellationToken);

        return Result.Deleted;
    }

    private static RingoGameDto ToDto(RingoGame game, GameCaller caller)
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
            RingoGameLobby.IsPlayer(game, caller) ? caller.PlayerId : null);
    }
}
