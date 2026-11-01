using PartyKing.Application.Ports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PartyKing.API.Authentication;
using PartyKing.API.Authorization;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Sessions;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Controllers;

public class SessionsController : CoreController
{
    public const string JoinRateLimitPolicy = "session-join";

    private readonly ISessionService _sessionService;
    private readonly ISessionAccessService _accessService;
    private readonly ISessionJoinTokenService _joinTokenService;
    private readonly ISessionAuthorizer _sessionAuthorizer;
    private readonly ICookieAuthorizationHandler _cookieAuthorizationHandler;

    public SessionsController(
        IHttpContextAccessor httpContextAccessor,
        ISessionService sessionService,
        ISessionAccessService accessService,
        ISessionJoinTokenService joinTokenService,
        ISessionAuthorizer sessionAuthorizer,
        ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor)
    {
        _sessionService = sessionService;
        _accessService = accessService;
        _joinTokenService = joinTokenService;
        _sessionAuthorizer = sessionAuthorizer;
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
        [FromBody] JoinSessionRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _accessService.JoinAsync(sessionId, request.Password, cancellationToken);

        if (result.IsError)
        {
            return Problem(result.Errors);
        }

        _cookieAuthorizationHandler.GrantSessionAccess(sessionId, result.Value, HttpContext);

        return NoContent();
    }

    /// <summary>
    /// Unlocks the session with a token from a QR code instead of the password. Shares the join rate limit; every
    /// failure is a plain 401.
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/join-with-token")]
    [EnableRateLimiting(JoinRateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> JoinWithToken(
        Guid sessionId,
        [FromBody] JoinWithTokenRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _joinTokenService.JoinWithTokenAsync(sessionId, request.Token, cancellationToken);

        if (result.IsError)
        {
            return Problem(result.Errors);
        }

        _cookieAuthorizationHandler.GrantSessionAccess(sessionId, result.Value, HttpContext);

        return NoContent();
    }

    /// <summary>Creates the short-lived token for the "Show QR code" link. Host or admin only (403 otherwise).</summary>
    [HttpPost("sessions/{sessionId:guid}/join-token")]
    [RequireSessionAccess]
    [ProducesResponseType<JoinTokenDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateJoinToken(Guid sessionId, CancellationToken cancellationToken)
    {
        var caller = await HttpContext.ReadSessionCallerAsync(_cookieAuthorizationHandler);

        var result = await _joinTokenService.CreateJoinTokenAsync(sessionId, caller, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Renames the session. Host or admin only (403 otherwise).</summary>
    [HttpPatch("sessions/{sessionId:guid}")]
    [RequireSessionAccess]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RenameSession(
        Guid sessionId,
        [FromBody] RenameSessionRequestDto request,
        CancellationToken cancellationToken)
    {
        var caller = await HttpContext.ReadSessionCallerAsync(_cookieAuthorizationHandler);

        var result = await _sessionService.RenameAsync(sessionId, request.Name, caller, cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }

    /// <summary>
    /// Sets a new password; everyone else has to enter it again to get back in, the caller stays unlocked. Host or
    /// admin only (403 otherwise).
    /// </summary>
    [HttpPut("sessions/{sessionId:guid}/password")]
    [RequireSessionAccess]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(
        Guid sessionId,
        [FromBody] ChangeSessionPasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        var caller = await HttpContext.ReadSessionCallerAsync(_cookieAuthorizationHandler);

        var result = await _accessService.ChangePasswordAsync(
            sessionId,
            request.Password,
            caller,
            cancellationToken);

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
        var caller = await HttpContext.ReadSessionCallerAsync(_cookieAuthorizationHandler);

        var result = await _sessionAuthorizer.GetViewerRoleAsync(sessionId, caller, cancellationToken);

        return result.Match(Ok, Problem);
    }
}
