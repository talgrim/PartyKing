using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PartyKing.API.Authorization;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Admin;
using PartyKing.Contract.V1.Sessions;

namespace PartyKing.API.Controllers;

public class AdminController : CoreController
{
    public const string LoginRateLimitPolicy = "admin-login";

    private readonly IAdminService _adminService;
    private readonly ISessionService _sessionService;

    public AdminController(
        IHttpContextAccessor httpContextAccessor,
        IAdminService adminService,
        ISessionService sessionService)
        : base(httpContextAccessor)
    {
        _adminService = adminService;
        _sessionService = sessionService;
    }

    [HttpPost("admin/login")]
    [EnableRateLimiting(LoginRateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _adminService.LoginAsync(request.Username, request.Password, cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }

    [HttpPost("admin/logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        _adminService.Logout();

        return NoContent();
    }

    /// <summary>Lets the admin panel check whether the admin cookie is still valid.</summary>
    [HttpGet("admin/me")]
    [AuthorizeAdmin]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult GetMe()
    {
        return NoContent();
    }

    [HttpPost("admin/sessions")]
    [AuthorizeAdmin]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreateSessionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sessionService.CreateAsync(request.Name, request.Password, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Deletes the session and its queue.</summary>
    [HttpDelete("admin/sessions/{sessionId:guid}")]
    [AuthorizeAdmin]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _sessionService.DeleteAsync(sessionId, cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }

    /// <summary>Removes the host of the session so that someone else can claim it.</summary>
    [HttpDelete("admin/sessions/{sessionId:guid}/host")]
    [AuthorizeAdmin]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReleaseSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _sessionService.ReleaseAsync(sessionId, cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }
}
