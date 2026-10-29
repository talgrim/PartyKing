using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PartyKing.API.Authorization;
using PartyKing.Application.Games.Services;
using PartyKing.Contract.V1.Games;
using PartyKing.Infrastructure.Authentication;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Controllers;

public class GamesController : CoreController
{
    public const string SuggestionsRateLimitPolicy = "game-suggestions";

    private readonly IGameService _gameService;
    private readonly IRingoService _ringoService;
    private readonly INeverHaveIEverSuggestionService _suggestionService;
    private readonly ICookieAuthorizationHandler _cookieAuthorizationHandler;

    public GamesController(
        IHttpContextAccessor httpContextAccessor,
        IGameService gameService,
        IRingoService ringoService,
        INeverHaveIEverSuggestionService suggestionService,
        ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor)
    {
        _gameService = gameService;
        _ringoService = ringoService;
        _suggestionService = suggestionService;
        _cookieAuthorizationHandler = cookieAuthorizationHandler;
    }

    /// <summary>Host or admin: the games available in the session. 403 for guests.</summary>
    [HttpGet("sessions/{sessionId:guid}/games")]
    [RequireSessionAccess]
    [ProducesResponseType<IReadOnlyList<GameDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGames(Guid sessionId, CancellationToken cancellationToken)
    {
        var profileId = _cookieAuthorizationHandler.TryReadProfileId(Request, out var id) ? id : null;
        var isAdmin = (await HttpContext.AuthenticateAsync(Constants.AdminScheme)).Succeeded;

        var result = await _gameService.ListAsync(sessionId, profileId, isAdmin, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Host or admin: spicy "Never have I ever" statements in Polish from the language model, or from a built-in list
    /// when it is unavailable. 403 for guests.
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/games/never-have-i-ever/suggestions")]
    [RequireSessionAccess]
    [EnableRateLimiting(SuggestionsRateLimitPolicy)]
    [ProducesResponseType<GameSuggestionsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNeverHaveIEverSuggestions(Guid sessionId, CancellationToken cancellationToken)
    {
        var profileId = _cookieAuthorizationHandler.TryReadProfileId(Request, out var id) ? id : null;
        var isAdmin = (await HttpContext.AuthenticateAsync(Constants.AdminScheme)).Succeeded;

        var result = await _suggestionService.SuggestAsync(sessionId, profileId, isAdmin, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: the session's Ringo game, 404 when there is none.</summary>
    [HttpGet("sessions/{sessionId:guid}/games/ringo")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRingo(Guid sessionId, CancellationToken cancellationToken)
    {
        var (profileId, isAdmin) = await ReadCallerAsync();

        var result = await _ringoService.GetAsync(sessionId, profileId, isAdmin, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: starts a Ringo game, 409 when the session already has one.</summary>
    [HttpPost("sessions/{sessionId:guid}/games/ringo")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> StartRingo(Guid sessionId, CancellationToken cancellationToken)
    {
        var (profileId, isAdmin) = await ReadCallerAsync();

        var result = await _ringoService.StartAsync(sessionId, profileId, isAdmin, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: flips a card (0-51), idempotent; 409 while another card is pending.</summary>
    [HttpPost("sessions/{sessionId:guid}/games/ringo/cards/{cardId:int}/flip")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> FlipRingoCard(Guid sessionId, int cardId, CancellationToken cancellationToken)
    {
        var (profileId, isAdmin) = await ReadCallerAsync();

        var result = await _ringoService.FlipAsync(sessionId, cardId, profileId, isAdmin, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: continues past the pending card, 404 when there is no game.</summary>
    [HttpPost("sessions/{sessionId:guid}/games/ringo/continue")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ContinueRingo(Guid sessionId, CancellationToken cancellationToken)
    {
        var (profileId, isAdmin) = await ReadCallerAsync();

        var result = await _ringoService.ContinueAsync(sessionId, profileId, isAdmin, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: ends the session's Ringo game, 404 when there is none.</summary>
    [HttpDelete("sessions/{sessionId:guid}/games/ringo")]
    [RequireSessionAccess]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> EndRingo(Guid sessionId, CancellationToken cancellationToken)
    {
        var (profileId, isAdmin) = await ReadCallerAsync();

        var result = await _ringoService.EndAsync(sessionId, profileId, isAdmin, cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }

    private async Task<(string? ProfileId, bool IsAdmin)> ReadCallerAsync()
    {
        var profileId = _cookieAuthorizationHandler.TryReadProfileId(Request, out var id) ? id : null;
        var isAdmin = (await HttpContext.AuthenticateAsync(Constants.AdminScheme)).Succeeded;

        return (profileId, isAdmin);
    }
}
