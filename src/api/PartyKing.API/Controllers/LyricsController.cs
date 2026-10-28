using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using PartyKing.API.Authorization;
using PartyKing.Application.Lyrics.Services;
using PartyKing.Contract.V1.Lyrics;
using PartyKing.Infrastructure.Authentication;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Controllers;

public class LyricsController : CoreController
{
    private readonly ILyricsService _lyricsService;
    private readonly ICookieAuthorizationHandler _cookieAuthorizationHandler;

    public LyricsController(
        IHttpContextAccessor httpContextAccessor,
        ILyricsService lyricsService,
        ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor)
    {
        _lyricsService = lyricsService;
        _cookieAuthorizationHandler = cookieAuthorizationHandler;
    }

    /// <summary>
    /// Host or admin: the synchronized lyrics of the song being played. 404 when nothing plays or no lyrics were
    /// found, 403 for guests.
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/now-playing/lyrics")]
    [RequireSessionAccess]
    [ProducesResponseType<LyricsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLyrics(Guid sessionId, CancellationToken cancellationToken)
    {
        var profileId = _cookieAuthorizationHandler.TryReadProfileId(Request, out var id) ? id : null;
        var isAdmin = (await HttpContext.AuthenticateAsync(Constants.AdminScheme)).Succeeded;

        var result = await _lyricsService.GetForPlayingSongAsync(sessionId, profileId, isAdmin, cancellationToken);

        return result.Match(Ok, Problem);
    }
}
