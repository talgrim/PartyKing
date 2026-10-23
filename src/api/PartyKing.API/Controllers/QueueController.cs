using Microsoft.AspNetCore.Mvc;
using PartyKing.API.Authorization;
using PartyKing.Application.Queue.Services;
using PartyKing.Contract.V1.Queue;

namespace PartyKing.API.Controllers;

public class QueueController : CoreController
{
    private readonly IQueueService _queueService;

    public QueueController(IHttpContextAccessor httpContextAccessor, IQueueService queueService)
        : base(httpContextAccessor)
    {
        _queueService = queueService;
    }

    [HttpPost("sessions/{sessionId:guid}/songs")]
    [ProducesResponseType<SongDataDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> QueueSong(
        Guid sessionId,
        [FromBody] EnqueueSongRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _queueService.EnqueueAsync(sessionId, request.Input, cancellationToken);

        return result.Match(Ok, Problem);
    }

    [HttpGet("sessions/{sessionId:guid}/songs")]
    [ProducesResponseType<IReadOnlyList<SongDataDto>>(StatusCodes.Status200OK)]
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
    [AuthorizeByCookie]
    [ProducesResponseType<SongDataDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNext(
        Guid sessionId,
        [FromQuery] int? currentSongId,
        CancellationToken cancellationToken)
    {
        var result = await _queueService.GetNextAsync(sessionId, GetProfileId(), currentSongId, cancellationToken);

        return result.Match(Ok, Problem);
    }
}
