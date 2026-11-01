using PartyKing.Application.Ports;
using Microsoft.AspNetCore.Mvc;
using PartyKing.API.Authentication;
using PartyKing.API.Authorization;
using PartyKing.Application.Queue.Services;
using PartyKing.Contract.V1.Queue;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Controllers;

public class QueueController : CoreController
{
    private readonly IQueueService _queueService;
    private readonly ICookieAuthorizationHandler _cookieAuthorizationHandler;

    public QueueController(
        IHttpContextAccessor httpContextAccessor,
        IQueueService queueService,
        ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor)
    {
        _queueService = queueService;
        _cookieAuthorizationHandler = cookieAuthorizationHandler;
    }

    [HttpPost("sessions/{sessionId:guid}/songs")]
    [RequireSessionAccess]
    [ProducesResponseType<QueueItemDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> QueueSong(
        Guid sessionId,
        [FromBody] EnqueueSongRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _queueService.EnqueueAsync(sessionId, request.Input, cancellationToken);

        return result.Match(Ok, Problem);
    }

    [HttpGet("sessions/{sessionId:guid}/songs")]
    [RequireSessionAccess]
    [ProducesResponseType<IReadOnlyList<QueueItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueue(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _queueService.GetQueueAsync(sessionId, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Marks the current song as played and starts the next one. Only the session's host may call it.
    /// 404 when the queue is empty. <paramref name="currentSongId"/> is the song the player thinks is playing (none
    /// when omitted); if an admin has skipped it meanwhile, nothing advances and the song that is playing is returned.
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/songs/next")]
    [RequireSessionAccess]
    [AuthorizeByCookie]
    [ProducesResponseType<QueueItemDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNext(
        Guid sessionId,
        [FromQuery] int? currentSongId,
        CancellationToken cancellationToken)
    {
        var result = await _queueService.GetNextAsync(sessionId, GetProfileId(), currentSongId, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Removes a song that is still waiting in the queue. Open to the session's host (signed host cookie) and to
    /// admins (admin cookie); 403 for everyone else, 409 when the song is already playing or played.
    /// </summary>
    [HttpDelete("sessions/{sessionId:guid}/songs/{songId:int}")]
    [RequireSessionAccess]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveSong(Guid sessionId, int songId, CancellationToken cancellationToken)
    {
        var caller = await HttpContext.ReadSessionCallerAsync(_cookieAuthorizationHandler);

        var result = await _queueService.RemoveAsync(sessionId, songId, caller, cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }
}
