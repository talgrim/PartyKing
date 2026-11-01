using PartyKing.Application.Ports;
using Microsoft.AspNetCore.Mvc;
using PartyKing.API.Authorization;
using PartyKing.Application.Games.Services;
using PartyKing.Contract.V1.Games;

namespace PartyKing.API.Controllers.Games;

/// <summary>The games of a session: <c>/sessions/{id}/games</c>.</summary>
[Route(GamesRoute)]
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
    public async Task<IActionResult> GetAll(Guid sessionId, CancellationToken cancellationToken)
    {
        var caller = await ReadCallerAsync(sessionId);

        var result = await _gameService.ListAsync(sessionId, caller, GetLanguage(), cancellationToken);

        return result.Match(Ok, Problem);
    }
}
