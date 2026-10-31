using Microsoft.AspNetCore.Mvc;
using PartyKing.API.Authorization;
using PartyKing.Application.Games.Services;
using PartyKing.Contract.V1.Games;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Controllers.Games.Resistance;

/// <summary>The rounds of a started Resistance game: team, vote, mission and the windows between them.</summary>
[Route(ResistanceRoute)]
public class ResistanceRoundController : ResistanceControllerBase
{
    public ResistanceRoundController(
        IHttpContextAccessor httpContextAccessor,
        IResistanceService resistanceService,
        ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor, resistanceService, cookieAuthorizationHandler)
    {
    }

    /// <summary>
    /// The mission leader submits the team (exactly the required number of distinct players), which locks it. 403 for
    /// anyone but the leader, 409 before the start or once a team was submitted, 400 for a wrong team.
    /// </summary>
    [HttpPut("team")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SelectTeam(
        Guid sessionId, [FromBody] ResistanceTeamRequest request, CancellationToken cancellationToken)
    {
        var result = await ResistanceService.SelectTeamAsync(
            sessionId, request.PlayerIds, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// A player votes on the submitted team and may change the vote until the last player has voted. 403 for anyone
    /// who did not join, 409 when no team is up for a vote.
    /// </summary>
    [HttpPut("vote")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Vote(
        Guid sessionId, [FromBody] ResistanceVoteRequest request, CancellationToken cancellationToken)
    {
        var result = await ResistanceService.VoteAsync(
            sessionId, request.Approve, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// A member of the approved team secretly plays Success or Fail, once. 403 for anyone who did not join, is not on
    /// the team, or is a member of the resistance playing Fail; 409 when no approved team is playing or the card was
    /// already played.
    /// </summary>
    [HttpPut("mission")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PlayMissionCard(
        Guid sessionId, [FromBody] ResistanceMissionCardRequest request, CancellationToken cancellationToken)
    {
        var result = await ResistanceService.PlayMissionCardAsync(
            sessionId, request.Success, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Moves on after a finished mission: the next player leads and picks a team for the next mission. The leader, the
    /// host and admins may; 409 unless the mission is over. A rejected team needs no step, the lead passes on at once.
    /// </summary>
    [HttpPost("continue")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Continue(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await ResistanceService.ContinueAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// A player confirms in the leader, confidence or peek window that they play no card; the last confirmation ends
    /// it. 403 for a host or admin who did not join, 409 outside a window.
    /// </summary>
    [HttpPut("leader-window/pass")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PassWindow(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await ResistanceService.PassLeaderWindowAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }
}
