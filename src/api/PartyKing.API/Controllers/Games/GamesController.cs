using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PartyKing.API.Authorization;
using PartyKing.Application.Games.Services;
using PartyKing.Contract.V1.Games;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Authentication;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Controllers.Games;

/// <summary>The games of a session: <c>/sessions/{id}/games</c>.</summary>
[Route(GameControllerBase.GamesRoute)]
public class GamesController : GameControllerBase
{
    private readonly IGameService _gameService;

    public GamesController(
        IHttpContextAccessor httpContextAccessor,
        IGameService gameService,
        ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor, cookieAuthorizationHandler)
    {
        _gameService = gameService;
    }

    /// <summary>
    /// Host or admin: the games available in the session, named in the <c>Accept-Language</c> language. 403 for guests.
    /// </summary>
    [HttpGet]
    [RequireSessionAccess]
    [ProducesResponseType<IReadOnlyList<GameDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGames(Guid sessionId, CancellationToken cancellationToken)
    {
        var profileId = Cookies.TryReadProfileId(Request, out var id) ? id : null;
        var isAdmin = (await HttpContext.AuthenticateAsync(Constants.AdminScheme)).Succeeded;

        var result = await _gameService.ListAsync(sessionId, profileId, isAdmin, GetLanguage(), cancellationToken);

        return result.Match(Ok, Problem);
    }
}
