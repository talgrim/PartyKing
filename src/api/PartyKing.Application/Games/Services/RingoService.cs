using ErrorOr;
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

    /// <summary>Flips a card (idempotent). Validation error for an id outside 0-51, NotFound without a game.</summary>
    Task<ErrorOr<RingoGameDto>> FlipAsync(Guid sessionId, int cardId, string? profileId, bool isAdmin, CancellationToken cancellationToken);

    /// <summary>Ends the game. NotFound when there is none.</summary>
    Task<ErrorOr<Deleted>> EndAsync(Guid sessionId, string? profileId, bool isAdmin, CancellationToken cancellationToken);
}

internal class RingoService : IRingoService
{
    public const int CardCount = 52;

    private readonly ISessionService _sessionService;
    private readonly IRingoGamesRepository _games;
    private readonly IDateTimeProvider _clock;

    public RingoService(ISessionService sessionService, IRingoGamesRepository games, IDateTimeProvider clock)
    {
        _sessionService = sessionService;
        _games = games;
        _clock = clock;
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

        return await _games.TryCreateAsync(game, cancellationToken)
            ? ToDto(game)
            : Error.Conflict("Ringo.AlreadyStarted", "The session already has a Ringo game.");
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

        var game = await _games.FlipAsync(sessionId, cardId, cancellationToken);

        return game is null ? NoGame : ToDto(game);
    }

    public async Task<ErrorOr<Deleted>> EndAsync(
        Guid sessionId, string? profileId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (await AuthorizeAsync(sessionId, profileId, isAdmin, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        return await _games.DeleteAsync(sessionId, cancellationToken) ? Result.Deleted : NoGame;
    }

    private static Error NoGame => Error.NotFound("Ringo.NotFound", "The session has no Ringo game.");

    private Task<ErrorOr<Success>> AuthorizeAsync(
        Guid sessionId, string? profileId, bool isAdmin, CancellationToken cancellationToken)
    {
        return _sessionService.AuthorizeManagerAsync(sessionId, profileId, isAdmin, cancellationToken);
    }

    private static RingoGameDto ToDto(RingoGame game)
    {
        var flipped = game.FlippedCards.Length == 0
            ? []
            : game.FlippedCards.Split(',').Select(int.Parse).ToList();

        return new RingoGameDto(game.Seed, flipped);
    }
}
