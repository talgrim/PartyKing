using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PartyKing.API.Authorization;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Sessions;
using PartyKing.Infrastructure.Authentication;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Controllers;

public class SessionsController : CoreController
{
    public const string JoinRateLimitPolicy = "session-join";

    private readonly ISessionService _sessionService;
    private readonly ICookieAuthorizationHandler _cookieAuthorizationHandler;

    public SessionsController(
        IHttpContextAccessor httpContextAccessor,
        ISessionService sessionService,
        ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor)
    {
        _sessionService = sessionService;
        _cookieAuthorizationHandler = cookieAuthorizationHandler;
    }

    [HttpGet("sessions")]
    [ProducesResponseType<IReadOnlyList<SessionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        return Ok(await _sessionService.GetAllAsync(cancellationToken));
    }

    /// <summary>
    /// Checks the session password and remembers the unlock in a cookie. 401 for a wrong password and for an
    /// unknown session alike, so it doesn't reveal which sessions exist.
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/join")]
    [EnableRateLimiting(JoinRateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Join(
        Guid sessionId,
        [FromBody] JoinSessionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sessionService.JoinAsync(sessionId, request.Password, cancellationToken);

        if (result.IsError)
        {
            return Problem(result.Errors);
        }

        _cookieAuthorizationHandler.GrantSessionAccess(sessionId, result.Value, HttpContext);

        return NoContent();
    }

    [HttpGet("sessions/{sessionId:guid}")]
    [RequireSessionAccess]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _sessionService.GetAsync(sessionId, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Tells the UI whether to show the player controls. Open to everyone: a viewer who is neither host nor admin
    /// just gets false for both.
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/me")]
    [RequireSessionAccess]
    [ProducesResponseType<ViewerRoleDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetViewerRole(Guid sessionId, CancellationToken cancellationToken)
    {
        // The signed cookie is enough to know who the host is; whether their Spotify token still works is a
        // separate matter, handled where the token is used
        var profileId = _cookieAuthorizationHandler.TryReadProfileId(Request, out var id) ? id : null;
        var isAdmin = (await HttpContext.AuthenticateAsync(Constants.AdminScheme)).Succeeded;

        var result = await _sessionService.GetViewerRoleAsync(sessionId, profileId, isAdmin, cancellationToken);

        return result.Match(Ok, Problem);
    }
}
