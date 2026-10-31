using Microsoft.AspNetCore.Mvc;
using PartyKing.API.Authorization;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Application.Games.Services;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Controllers.Games.Resistance;

/// <summary>The plot cards add-on of a Resistance game: switching it on, dealing and playing the cards.</summary>
[Route(ResistanceRoute)]
public class ResistancePlotCardsController : ResistanceControllerBase
{
    public ResistancePlotCardsController(
        IHttpContextAccessor httpContextAccessor,
        IResistancePlotCardService plotCardService,
        IResistanceLobbyService lobbyService,
        ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor, cookieAuthorizationHandler)
    {
        PlotCards = plotCardService;
        Lobby = lobbyService;
    }

    private IResistancePlotCardService PlotCards { get; }

    private IResistanceLobbyService Lobby { get; }

    /// <summary>Host or admin: switches the plot cards add-on on or off. 409 once the game is open or started.</summary>
    [HttpPut("rules/plot-cards")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetEnabled(
        Guid sessionId, [FromBody] ResistancePlotCardsRequestDto request, CancellationToken cancellationToken)
    {
        var result = await Lobby.SetPlotCardsAsync(
            sessionId, request.Enabled, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// The mission leader hands the plot cards they drew to the other players, which moves on to picking the team.
    /// 403 for anyone else, 409 when nothing is to be handed out, 400 for a wrong or incomplete deal.
    /// </summary>
    [HttpPut("cards/deal")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Deal(
        Guid sessionId, [FromBody] ResistanceDealPlotCardsRequestDto request, CancellationToken cancellationToken)
    {
        var result = await PlotCards.DealPlotCardsAsync(
            sessionId, request.Assignments, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// The holder of an immediate plot card plays it on a player. 403 for anyone else, 404 for an unknown card,
    /// 409 when the cards are not being played or it was played already, 400 for a target the card can't be played on.
    /// </summary>
    [HttpPut("cards/{cardId:int}/play")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Play(
        Guid sessionId, int cardId, [FromBody] ResistancePlayPlotCardRequestDto request, CancellationToken cancellationToken)
    {
        var result = await PlotCards.PlayPlotCardAsync(
            sessionId, cardId, request.TargetPlayerId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// The player an identity card was shown to confirms having seen it; the last confirmation moves on to picking the
    /// team. 403 for anyone else, 409 when the card has not been played or is confirmed already.
    /// </summary>
    [HttpPut("cards/{cardId:int}/confirm")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Confirm(Guid sessionId, int cardId, CancellationToken cancellationToken)
    {
        var result = await PlotCards.ConfirmPlotCardAsync(
            sessionId, cardId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// The holder of Strong Leader takes the lead in the leader window. 403 for anyone else, 404 for an unknown card,
    /// 409 outside the window or once played, 400 when the holder already leads.
    /// </summary>
    [HttpPut("cards/{cardId:int}/take-lead")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> TakeLead(Guid sessionId, int cardId, CancellationToken cancellationToken)
    {
        var result = await PlotCards.TakeLeadAsync(sessionId, cardId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// The holder of Keeping a Close Eye looks at a team member's mission card once the mission is played (until
    /// "Dalej"). 403 for anyone else, 404 for an unknown card, 409 outside that step or once played, 400 for your own
    /// card, a player who did not play or a card someone already looked at.
    /// </summary>
    [HttpPut("cards/{cardId:int}/peek")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PeekMissionCard(
        Guid sessionId, int cardId, [FromBody] ResistancePlayPlotCardRequestDto request, CancellationToken cancellationToken)
    {
        var result = await PlotCards.PeekMissionCardAsync(
            sessionId, cardId, request.TargetPlayerId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// The holder of No Confidence cancels the approved team in the confidence window; it counts as a rejected team.
    /// 403 for anyone else, 404 for an unknown card, 409 outside the window or once played.
    /// </summary>
    [HttpPut("cards/{cardId:int}/no-confidence")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PlayNoConfidence(Guid sessionId, int cardId, CancellationToken cancellationToken)
    {
        var result = await PlotCards.PlayNoConfidenceAsync(
            sessionId, cardId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// The holder of Take Responsibility takes a plot card from another player. 403 for anyone else, 404 for an unknown
    /// card, 409 when it was played already or the round is in a step where it can't be, 400 for a bad target or a
    /// missing choice between the Opinion Maker and a hand card.
    /// </summary>
    [HttpPut("cards/{cardId:int}/take")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> TakeResponsibility(
        Guid sessionId, int cardId, [FromBody] ResistanceTakeResponsibilityRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await PlotCards.TakeResponsibilityAsync(
            sessionId, cardId, request.TargetPlayerId, request.TakeOpinionMaker, await Caller(sessionId),
            cancellationToken);

        return result.Match(Ok, Problem);
    }
}
