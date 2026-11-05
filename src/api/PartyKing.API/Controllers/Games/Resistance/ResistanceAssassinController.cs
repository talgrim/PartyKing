using Microsoft.AspNetCore.Mvc;
using PartyKing.API.Authorization;
using PartyKing.API.Cookies;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Contract.V1.Games.Resistance;

namespace PartyKing.API.Controllers.Games.Resistance;

/// <summary>The Assassin module of a Resistance game: switching it on and naming the Commander.</summary>
[Route(ResistanceRoute)]
public class ResistanceAssassinController : ResistanceControllerBase
{
    public ResistanceAssassinController(
        IHttpContextAccessor httpContextAccessor,
        IResistanceAssassinService assassinService,
        IResistanceLobbyService lobbyService,
        ISignedCookies cookies)
        : base(httpContextAccessor, cookies)
    {
        Assassin = assassinService;
        Lobby = lobbyService;
    }

    private IResistanceAssassinService Assassin { get; }

    private IResistanceLobbyService Lobby { get; }

    /// <summary>
    /// Host or admin: switches the Assassin module on or off and, while it is on, its optional characters (omitted
    /// keeps the choice). 409 once the game has started or while the Defector, the Reverser or Blind Spies are on.
    /// </summary>
    [HttpPut("rules/assassin")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetEnabled(
        Guid sessionId, [FromBody] ResistanceAssassinRequestDto request, CancellationToken cancellationToken)
    {
        var result = await Lobby.SetAssassinAsync(
            sessionId,
            request.Enabled,
            request.BodyGuard,
            request.DeepCover,
            request.BlindSpy,
            request.FalseCommander,
            await Caller(sessionId),
            cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// The Assassin names the player they take for the Commander, which ends the game. 403 for anyone else, 409
    /// outside the Assassination, 400 for a player who can't be named.
    /// </summary>
    [HttpPut("assassin/target")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Assassinate(
        Guid sessionId, [FromBody] ResistanceAssassinateRequestDto request, CancellationToken cancellationToken)
    {
        var result = await Assassin.AssassinateAsync(
            sessionId, request.TargetPlayerId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }
}
