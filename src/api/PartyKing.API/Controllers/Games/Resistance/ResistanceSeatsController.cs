using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PartyKing.API.Authorization;
using PartyKing.API.Cookies;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Contract.V1.Games.Resistance;

namespace PartyKing.API.Controllers.Games.Resistance;

/// <summary>
/// Taking a seat over in a started Resistance game: a player whose device is gone (a closed private tab) returns
/// through a seat the host or an admin released.
/// </summary>
[Route(ResistanceRoute)]
public class ResistanceSeatsController : ResistanceControllerBase
{
    private readonly IResistanceSeatService _seats;

    public ResistanceSeatsController(
        IHttpContextAccessor httpContextAccessor,
        IResistanceSeatService seats,
        ISignedCookies cookies)
        : base(httpContextAccessor, cookies)
    {
        _seats = seats;
    }

    /// <summary>
    /// Anyone who unlocked the session: the seats of the started game a guest may take over, those released by the
    /// host or an admin whose player has no device connected. Empty before the start, 404 without a game.
    /// </summary>
    [HttpGet("claimable-seats")]
    [RequireSessionAccess]
    [ProducesResponseType<IReadOnlyList<ResistanceClaimableSeatDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClaimable(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _seats.GetClaimableAsync(sessionId, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Host or admin: releases the seat of an offline player of the started game for a guest to take over, or takes
    /// the release back. 409 for the seat of the host or an admin, for a player who is online and before the start.
    /// </summary>
    [HttpPut("players/{playerId:guid}/release")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetReleased(
        Guid sessionId,
        Guid playerId,
        [FromBody] ResistanceSeatReleaseRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _seats.SetReleasedAsync(
            sessionId, playerId, request.Released, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Takes over a released seat and remembers it in the player cookie with a new stamp, which cuts the old device off.
    /// 409 for a seat that was not released or whose player is connected.
    /// </summary>
    [HttpPost("claim")]
    [RequireSessionAccess]
    [EnableRateLimiting(SessionsController.JoinRateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Claim(
        Guid sessionId, [FromBody] ResistanceClaimSeatRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _seats.ClaimAsync(sessionId, request.PlayerId, cancellationToken);
        if (!result.IsError)
        {
            Cookies.GrantGamePlayer(Kind, sessionId, result.Value, HttpContext);
        }

        return result.Match(_ => NoContent(), Problem);
    }
}
