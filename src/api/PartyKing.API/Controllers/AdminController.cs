using Microsoft.AspNetCore.Mvc;
using PartyKing.API.Authorization;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Sessions;

namespace PartyKing.API.Controllers;

public class AdminController : CoreController
{
    private readonly ISessionAdminService _sessionAdminService;

    public AdminController(
        IHttpContextAccessor httpContextAccessor,
        ISessionAdminService sessionAdminService)
        : base(httpContextAccessor)
    {
        _sessionAdminService = sessionAdminService;
    }

    /// <summary>Every session with its host's name.</summary>
    [HttpGet("admin/sessions")]
    [AuthorizeAdmin]
    [ProducesResponseType<IReadOnlyList<SessionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        return Ok(await _sessionAdminService.GetAllAsync(cancellationToken));
    }

    [HttpPost("admin/sessions")]
    [AuthorizeAdmin]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreateSessionRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _sessionAdminService.CreateAsync(request.Name, request.Password, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Deletes the session and its queue.</summary>
    [HttpDelete("admin/sessions/{sessionId:guid}")]
    [AuthorizeAdmin]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _sessionAdminService.DeleteAsync(sessionId, cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }

    /// <summary>
    /// Calls off the deletion a guest asked for; the session stays. 204 also when none was scheduled. The host cannot
    /// do this, only delete the session at once.
    /// </summary>
    [HttpDelete("admin/sessions/{sessionId:guid}/deletion-request")]
    [AuthorizeAdmin]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CancelSessionDeletion(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _sessionAdminService.CancelDeletionAsync(sessionId, cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }

    /// <summary>Removes the host of the session so that someone else can claim it.</summary>
    [HttpDelete("admin/sessions/{sessionId:guid}/host")]
    [AuthorizeAdmin]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReleaseSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _sessionAdminService.ReleaseAsync(sessionId, cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }
}
