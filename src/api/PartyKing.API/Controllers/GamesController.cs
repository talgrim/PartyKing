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
    private readonly INeverHaveIEverSuggestionService _suggestionService;
    private readonly ICookieAuthorizationHandler _cookieAuthorizationHandler;

    public GamesController(
        IHttpContextAccessor httpContextAccessor,
        IGameService gameService,
        INeverHaveIEverSuggestionService suggestionService,
        ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor)
    {
        _gameService = gameService;
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
}
