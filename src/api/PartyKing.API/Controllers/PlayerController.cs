using Microsoft.AspNetCore.Mvc;
using PartyKing.API.Authorization;
using PartyKing.Application.Player.Services;
using PartyKing.Contract.V1.Player;
using PartyKing.Contract.V1.Queue;

namespace PartyKing.API.Controllers;

public class PlayerController : CoreController
{
    private readonly IPlayerService _playerService;

    public PlayerController(IHttpContextAccessor httpContextAccessor, IPlayerService playerService)
        : base(httpContextAccessor)
    {
        _playerService = playerService;
    }

    [HttpGet("sessions/{sessionId:guid}/now-playing")]
    [ProducesResponseType<NowPlayingDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNowPlaying(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _playerService.GetNowPlayingAsync(sessionId, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// The host's player reports what it is doing every few seconds, which is also how the server knows a player
    /// is connected. The response carries the command an admin sent in the meantime.
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/player/state")]
    [AuthorizeByCookie]
    [ProducesResponseType<PlayerStateResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReportState(
        Guid sessionId,
        [FromBody] PlayerStateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _playerService.ReportStateAsync(sessionId, GetProfileId(), request, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Asks the host's player to pause or resume. 409 when no player is connected.</summary>
    [HttpPost("admin/sessions/{sessionId:guid}/player/commands")]
    [AuthorizeAdmin]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SendCommand(
        Guid sessionId,
        [FromBody] PlayerCommandRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _playerService.SendCommandAsync(sessionId, request.Command, cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }

    /// <summary>
    /// Skips to the next song; the host's player picks it up from now-playing. 409 when no player is connected,
    /// 404 when the queue is empty.
    /// </summary>
    [HttpPost("admin/sessions/{sessionId:guid}/songs/next")]
    [AuthorizeAdmin]
    [ProducesResponseType<SongDataDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Skip(
        Guid sessionId,
        [FromQuery] int? currentSongId,
        CancellationToken cancellationToken)
    {
        var result = await _playerService.SkipAsync(sessionId, currentSongId, cancellationToken);

        return result.Match(Ok, Problem);
    }
}
