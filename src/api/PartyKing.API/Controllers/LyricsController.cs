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
    /// The synchronized lyrics of the song being played, for anyone who unlocked the session. 404 when nothing
    /// plays or no lyrics were found.
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/now-playing/lyrics")]
    [RequireSessionAccess]
    [ProducesResponseType<LyricsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLyrics(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _lyricsService.GetForPlayingSongAsync(sessionId, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Saves the lyrics timing offset of the song being played, for every session that plays it, and pushes it to
    /// the session's viewers. Host or admin only (403 otherwise); 404 when nothing is playing.
    /// </summary>
    [HttpPut("sessions/{sessionId:guid}/now-playing/lyrics/offset")]
    [RequireSessionAccess]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetOffset(
        Guid sessionId,
        [FromBody] SetLyricsOffsetRequestDto request,
        CancellationToken cancellationToken)
    {
        var profileId = _cookieAuthorizationHandler.TryReadProfileId(Request, out var id) ? id : null;
        var isAdmin = (await HttpContext.AuthenticateAsync(Constants.AdminScheme)).Succeeded;

        var result = await _lyricsService.SetOffsetAsync(
            sessionId,
            request.OffsetMs,
            profileId,
            isAdmin,
            cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }
}
