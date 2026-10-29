using ErrorOr;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Games;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Games.Services;

public interface IRingoService
{
    /// <summary>The session's game. NotFound when there is none; Forbidden for guests.</summary>
    Task<ErrorOr<RingoGameDto>> GetAsync(Guid sessionId, string? profileId, bool isAdmin, CancellationToken cancellationToken);

    /// <summary>Starts a game with a random layout. Conflict when the session already has one.</summary>
    Task<ErrorOr<RingoGameDto>> StartAsync(Guid sessionId, string? profileId, bool isAdmin, CancellationToken cancellationToken);

    /// <summary>
    /// Flips a card and makes it the pending one (idempotent for a flipped card). Validation error for an id outside
    /// 0-51, NotFound without a game, Conflict while another card is pending or after the fourth king.
    /// </summary>
    Task<ErrorOr<RingoGameDto>> FlipAsync(Guid sessionId, int cardId, string? profileId, bool isAdmin, CancellationToken cancellationToken);

    /// <summary>Clears the pending card (idempotent). NotFound without a game.</summary>
    Task<ErrorOr<RingoGameDto>> ContinueAsync(Guid sessionId, string? profileId, bool isAdmin, CancellationToken cancellationToken);

    /// <summary>Ends the game. NotFound when there is none.</summary>
    Task<ErrorOr<Deleted>> EndAsync(Guid sessionId, string? profileId, bool isAdmin, CancellationToken cancellationToken);
}

internal class RingoService : IRingoService
{
    public const int CardCount = 52;

    private readonly ISessionService _sessionService;
    private readonly IRingoGamesRepository _games;
    private readonly IDateTimeProvider _clock;
    private readonly ISessionMusicNotifier _notifier;

    public RingoService(
        ISessionService sessionService,
        IRingoGamesRepository games,
        IDateTimeProvider clock,
        ISessionMusicNotifier notifier)
    {
        _sessionService = sessionService;
        _games = games;
        _clock = clock;
        _notifier = notifier;
    }

    public async Task<ErrorOr<RingoGameDto>> GetAsync(
        Guid sessionId, string? profileId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(sessionId, profileId, isAdmin, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        var game = await _games.GetAsync(sessionId, cancellationToken);

        return game is null ? NoGame : ToDto(game);
    }

    public async Task<ErrorOr<RingoGameDto>> StartAsync(
        Guid sessionId, string? profileId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(sessionId, profileId, isAdmin, cancellationToken) is { IsError: true } denied)
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

        return ToDto(game);
    }

    public async Task<ErrorOr<RingoGameDto>> FlipAsync(
        Guid sessionId, int cardId, string? profileId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(sessionId, profileId, isAdmin, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        if (cardId is < 0 or >= CardCount)
        {
            return Error.Validation("Ringo.InvalidCard", $"The card id must be between 0 and {CardCount - 1}.");
        }

        // Names are placeholders until the real host and admin names are shown
        var flippedBy = isAdmin ? "admin" : "guest";
        var result = await _games.FlipAsync(sessionId, cardId, flippedBy, _clock.UtcNow, cancellationToken);

        switch (result.Status)
        {
            case RingoFlipStatus.NoGame:
                return NoGame;
            case RingoFlipStatus.GameOver:
                return Error.Conflict("Ringo.GameOver", "The fourth king ended the game.");
            case RingoFlipStatus.CardPending:
                return Error.Conflict("Ringo.CardPending", "Continue past the flipped card before flipping another.");
            case RingoFlipStatus.Flipped:
                await _notifier.RingoChangedAsync(sessionId, cancellationToken);
                break;
        }

        return ToDto(result.Game!);
    }

    public async Task<ErrorOr<RingoGameDto>> ContinueAsync(
        Guid sessionId, string? profileId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(sessionId, profileId, isAdmin, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        var game = await _games.ContinueAsync(sessionId, cancellationToken);
        if (game is null)
        {
            return NoGame;
        }

        await _notifier.RingoChangedAsync(sessionId, cancellationToken);

        return ToDto(game);
    }

    public async Task<ErrorOr<Deleted>> EndAsync(
        Guid sessionId, string? profileId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(sessionId, profileId, isAdmin, cancellationToken) is { IsError: true } denied)
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

    private static Error NoGame => Error.NotFound("Ringo.NotFound", "The session has no Ringo game.");

    private Task<ErrorOr<Success>> AuthorizeAsync(
        Guid sessionId, string? profileId, bool isAdmin, CancellationToken cancellationToken)
    {
        return _sessionService.AuthorizeManagerAsync(sessionId, profileId, isAdmin, cancellationToken);
    }

    private static RingoGameDto ToDto(RingoGame game)
    {
        var flips = game.Flips
            .OrderBy(x => x.Order)
            .Select(x => new RingoFlipDto(x.CardId, x.FlippedAt, x.FlippedBy))
            .ToList();

        return new RingoGameDto(game.Seed, flips, game.PendingCardId);
    }
}
