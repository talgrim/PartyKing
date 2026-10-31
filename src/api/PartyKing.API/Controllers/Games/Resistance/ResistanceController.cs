using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PartyKing.API.Authorization;
using PartyKing.Application.Games.Services;
using PartyKing.Contract.V1.Games;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Controllers.Games;

[Route(GamesRoute + "/resistance")]
public class ResistanceController : GameControllerBase
{
    private readonly IResistanceService _resistanceService;

    public ResistanceController(
        IHttpContextAccessor httpContextAccessor,
        IResistanceService resistanceService,
        ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor, cookieAuthorizationHandler)
    {
        _resistanceService = resistanceService;
    }

    /// <summary>
    /// The session's Resistance game. Anyone who unlocked the session may watch it once it is open; until then it is
    /// 404 for everyone but the host and admins, and 404 when there is no game.
    /// </summary>
    [HttpGet]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetResistance(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.GetAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Anyone who unlocked the session: whether the Resistance game is open to join and whether the caller did.</summary>
    [HttpGet("lobby")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceLobbyDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetResistanceLobby(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.GetLobbyAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: creates the Resistance game (closed to guests), 409 when the session already has one.</summary>
    [HttpPost]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateResistance(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.CreateAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Host or admin: starts a finished Resistance game over with the same players (new roles and leader, mission 1).
    /// 409 until the game is over.
    /// </summary>
    [HttpPost("restart")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RestartResistance(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.RestartAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Host or admin: replaces the game's rules (spies, team sizes, fails needed) with the current server
    /// configuration. 409 once the game is open or started.
    /// </summary>
    [HttpPost("rules/refresh")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshResistanceRules(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.RefreshRulesAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: switches the plot cards add-on on or off. 409 once the game is open or started.</summary>
    [HttpPut("rules/plot-cards")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetResistancePlotCards(
        Guid sessionId, [FromBody] ResistancePlotCardsRequest request, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.SetPlotCardsAsync(
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
    public async Task<IActionResult> DealResistancePlotCards(
        Guid sessionId, [FromBody] ResistanceDealPlotCardsRequest request, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.DealPlotCardsAsync(
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
    public async Task<IActionResult> PlayResistancePlotCard(
        Guid sessionId, int cardId, [FromBody] ResistancePlayPlotCardRequest request, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.PlayPlotCardAsync(
            sessionId, cardId, request.TargetPlayerId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// The holder of Strong Leader takes the lead in the leader window. 403 for anyone else, 404 for an unknown card,
    /// 409 outside the window or once played, 400 when the holder already leads.
    /// </summary>
    [HttpPut("cards/{cardId:int}/take-lead")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> TakeResistanceLead(
        Guid sessionId, int cardId, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.TakeLeadAsync(sessionId, cardId, await Caller(sessionId), cancellationToken);

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
    public async Task<IActionResult> PeekResistanceMissionCard(
        Guid sessionId, int cardId, ResistancePlayPlotCardRequest request, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.PeekMissionCardAsync(
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
    public async Task<IActionResult> PlayResistanceNoConfidence(
        Guid sessionId, int cardId, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.PlayNoConfidenceAsync(sessionId, cardId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// A player confirms in the leader or confidence window that they play no card; the last confirmation ends it. 403 for a host or
    /// admin who did not join, 409 outside the window.
    /// </summary>
    [HttpPut("leader-window/pass")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PassResistanceLeaderWindow(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.PassLeaderWindowAsync(sessionId, await Caller(sessionId), cancellationToken);

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
    public async Task<IActionResult> TakeResistanceResponsibility(
        Guid sessionId, int cardId, [FromBody] ResistanceTakeResponsibilityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _resistanceService.TakeResponsibilityAsync(
            sessionId, cardId, request.TargetPlayerId, request.TakeOpinionMaker, await Caller(sessionId),
            cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// The player an identity card was shown to confirms having seen it; the last confirmation moves on to picking the
    /// team. 403 for anyone else, 409 when the card has not been played or is confirmed already.
    /// </summary>
    [HttpPut("cards/{cardId:int}/confirm")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmResistancePlotCard(
        Guid sessionId, int cardId, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.ConfirmPlotCardAsync(
            sessionId, cardId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Admin only: opens the Resistance game to guests (idempotent), 403 for the host.</summary>
    [HttpPost("open")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> OpenResistance(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.OpenAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Host or admin: starts the open Resistance game (deals the roles, picks the first leader, locks the players).
    /// 409 while it is closed or already started or has fewer than 5 players.
    /// </summary>
    [HttpPost("start")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> StartResistance(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.StartAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Joins the open Resistance game as a player and remembers the player in a cookie. 400 for a bad name, 409 while
    /// the game is closed or full or the name is taken. The session host's name comes from their Spotify profile, an
    /// admin without a name joins as "admin".
    /// </summary>
    [HttpPost("join")]
    [RequireSessionAccess]
    [EnableRateLimiting(SessionsController.JoinRateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> JoinResistance(
        Guid sessionId, [FromBody] ResistanceJoinRequest request, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.JoinAsync(
            sessionId, request.Name, await Caller(sessionId), cancellationToken);
        if (!result.IsError)
        {
            Cookies.GrantGamePlayer(GameKind.Resistance, sessionId, result.Value, HttpContext);
        }

        return result.Match(_ => NoContent(), Problem);
    }

    /// <summary>
    /// The mission leader submits the team (exactly the required number of distinct players), which locks it. 403 for
    /// anyone but the leader, 409 before the start or once a team was submitted, 400 for a wrong team.
    /// </summary>
    [HttpPut("team")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SelectResistanceTeam(
        Guid sessionId, [FromBody] ResistanceTeamRequest request, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.SelectTeamAsync(
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
    public async Task<IActionResult> VoteResistanceTeam(
        Guid sessionId, [FromBody] ResistanceVoteRequest request, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.VoteAsync(
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
    public async Task<IActionResult> PlayResistanceMissionCard(
        Guid sessionId, [FromBody] ResistanceMissionCardRequest request, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.PlayMissionCardAsync(
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
    public async Task<IActionResult> ContinueResistance(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.ContinueAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: removes a player from the Resistance game, 404 for an unknown game or player.</summary>
    [HttpDelete("players/{playerId:guid}")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> KickResistancePlayer(
        Guid sessionId, Guid playerId, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.KickAsync(
            sessionId, playerId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: sets the seating order; 409 when the ids are not exactly the players.</summary>
    [HttpPut("players/order")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReorderResistancePlayers(
        Guid sessionId, [FromBody] ResistancePlayersOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.ReorderAsync(
            sessionId, request.PlayerIds, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: ends the Resistance game, 404 when there is none.</summary>
    [HttpDelete]
    [RequireSessionAccess]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> EndResistance(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _resistanceService.EndAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }

    private Task<GameCaller> Caller(Guid sessionId) => ReadCallerAsync(sessionId, GameKind.Resistance);
}
