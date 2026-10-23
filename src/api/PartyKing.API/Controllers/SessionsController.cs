using Microsoft.AspNetCore.Mvc;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Sessions;

namespace PartyKing.API.Controllers;

public class SessionsController : CoreController
{
    private readonly ISessionService _sessionService;

    public SessionsController(IHttpContextAccessor httpContextAccessor, ISessionService sessionService)
        : base(httpContextAccessor)
    {
        _sessionService = sessionService;
    }

    [HttpGet("sessions")]
    [ProducesResponseType<IReadOnlyList<SessionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        return Ok(await _sessionService.GetAllAsync(cancellationToken));
    }

    [HttpGet("sessions/{sessionId:guid}")]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _sessionService.GetAsync(sessionId, cancellationToken);

        return result.Match(Ok, Problem);
    }
}
