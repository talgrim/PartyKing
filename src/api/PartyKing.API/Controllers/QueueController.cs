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

    [HttpPost("queue/songs")]
    [ProducesResponseType<SongDataDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> QueueSong(
        [FromBody] EnqueueSongRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _queueService.EnqueueAsync(request.Input, cancellationToken);

        return result.Match(Ok, Problem);
    }

    [HttpGet("queue/songs")]
    [ProducesResponseType<IReadOnlyList<SongDataDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQueue(CancellationToken cancellationToken)
    {
        return Ok(await _queueService.GetQueueAsync(cancellationToken));
    }

    /// <summary>Marks the current song as played and starts the next one. 404 when the queue is empty.</summary>
    [HttpPost("queue/songs/next")]
    [AuthorizeByCookie]
    [ProducesResponseType<SongDataDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNext(CancellationToken cancellationToken)
    {
        var result = await _queueService.GetNextAsync(cancellationToken);

        return result.Match(Ok, Problem);
    }
}
