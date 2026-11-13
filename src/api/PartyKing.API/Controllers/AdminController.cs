using Microsoft.AspNetCore.Mvc;
using PartyKing.API.Authorization;
using PartyKing.Application.Accounts.Services;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Accounts;
using PartyKing.Contract.V1.Common;
using PartyKing.Contract.V1.Sessions;

namespace PartyKing.API.Controllers;

public class AdminController : CoreController
{
    private readonly IAccountAdminService _accountAdminService;
    private readonly ISessionAdminService _sessionAdminService;

    public AdminController(
        IHttpContextAccessor httpContextAccessor,
        IAccountAdminService accountAdminService,
        ISessionAdminService sessionAdminService)
        : base(httpContextAccessor)
    {
        _accountAdminService = accountAdminService;
        _sessionAdminService = sessionAdminService;
    }

    /// <summary>One page of the registered users (never the admins), ordered by username.</summary>
    [HttpGet("admin/users")]
    [AuthorizeAdmin]
    [ProducesResponseType<PagedDto<AccountDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListUsers(
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await _accountAdminService.ListUsersAsync(search, page, pageSize, cancellationToken);

        return Ok(result);
    }

    /// <summary>Sets a new password for a user, who is signed out everywhere. Admin passwords are not reset here.</summary>
    [HttpPut("admin/users/{accountId:guid}/password")]
    [AuthorizeAdmin]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(
        Guid accountId,
        [FromBody] ResetPasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _accountAdminService.ResetPasswordAsync(accountId, request.NewPassword, cancellationToken);

        return result.Match(_ => NoContent(), Problem);
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
