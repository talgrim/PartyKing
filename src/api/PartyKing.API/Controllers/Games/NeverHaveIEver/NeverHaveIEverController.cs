using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PartyKing.API.Authorization;
using PartyKing.Application.Games.Services;
using PartyKing.Contract.V1.Games;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Controllers.Games;

[Route(GamesRoute + "/never-have-i-ever")]
public class NeverHaveIEverController : GameControllerBase
{
    private readonly INeverHaveIEverSuggestionService _suggestionService;

    public NeverHaveIEverController(
        IHttpContextAccessor httpContextAccessor,
        INeverHaveIEverSuggestionService suggestionService,
        ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor, cookieAuthorizationHandler)
    {
        _suggestionService = suggestionService;
    }

    /// <summary>
    /// Host or admin: spicy "Never have I ever" statements from the language model, in the <c>Accept-Language</c>
    /// language, or from a built-in list when it is unavailable. 403 for guests.
    /// </summary>
    [HttpGet("suggestions")]
    [RequireSessionAccess]
    [EnableRateLimiting(SuggestionsRateLimitPolicy)]
    [ProducesResponseType<GameSuggestionsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuggestions(Guid sessionId, CancellationToken cancellationToken)
    {
        var caller = await ReadCallerAsync(sessionId);

        var result = await _suggestionService.SuggestAsync(
            sessionId, caller.ProfileId, caller.IsAdmin, GetLanguage(), cancellationToken);

        return result.Match(Ok, Problem);
    }
}
