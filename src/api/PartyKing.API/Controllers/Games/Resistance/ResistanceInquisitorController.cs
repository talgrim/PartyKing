using PartyKing.API.Cookies;
using Microsoft.AspNetCore.Mvc;
using PartyKing.API.Authorization;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Contract.V1.Games.Resistance;

namespace PartyKing.API.Controllers.Games.Resistance;

/// <summary>The Inquisitor module of a Resistance game: switching it on and examining a player.</summary>
[Route(ResistanceRoute)]
public class ResistanceInquisitorController : ResistanceControllerBase
{
    public ResistanceInquisitorController(
        IHttpContextAccessor httpContextAccessor,
        IResistanceInquisitorService inquisitorService,
        IResistanceLobbyService lobbyService,
        ISignedCookies cookies)
        : base(httpContextAccessor, cookies)
    {
        Inquisitor = inquisitorService;
        Lobby = lobbyService;
    }

    private IResistanceInquisitorService Inquisitor { get; }

    private IResistanceLobbyService Lobby { get; }

    /// <summary>Host or admin: switches the Inquisitor module on or off. 409 once the game has started.</summary>
    [HttpPut("rules/inquisitor")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetEnabled(
        Guid sessionId, [FromBody] ResistanceInquisitorRequestDto request, CancellationToken cancellationToken)
    {
        var result = await Lobby.SetInquisitorAsync(
            sessionId, request.Enabled, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// The holder of the Inquisitor token picks a player to examine; the token passes to them. 403 for anyone else,
    /// 409 outside the Inquisition or after picking, 400 for a player who can't be examined.
    /// </summary>
    [HttpPut("inquisitor/examine")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Examine(
        Guid sessionId, [FromBody] ResistanceExamineRequestDto request, CancellationToken cancellationToken)
    {
        var result = await Inquisitor.ExamineAsync(
            sessionId, request.TargetPlayerId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// The examiner confirms having seen the loyalty, which ends the Inquisition. 403 for anyone else, 409 before a
    /// player was picked or outside the Inquisition.
    /// </summary>
    [HttpPut("inquisitor/confirm")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Confirm(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await Inquisitor.ConfirmInquisitionAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }
}
