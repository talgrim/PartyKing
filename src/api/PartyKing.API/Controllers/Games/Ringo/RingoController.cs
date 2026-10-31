using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PartyKing.API.Authorization;
using PartyKing.Application.Games.Services.Ringo;
using PartyKing.Contract.V1.Games;
using PartyKing.Contract.V1.Games.Ringo;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Controllers.Games.Ringo;

[Route(GamesRoute + "/ringo")]
public class RingoController : LobbyGameControllerBase
{
    private readonly IRingoService _ringoService;
    private readonly IRingoRuleSuggestionService _ruleSuggestionService;

    public RingoController(
        IHttpContextAccessor httpContextAccessor,
        IRingoService ringoService,
        IRingoRuleSuggestionService ruleSuggestionService,
        ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor, cookieAuthorizationHandler)
    {
        _ringoService = ringoService;
        _ruleSuggestionService = ruleSuggestionService;
    }

    protected override GameKind Kind => GameKind.Ringo;

    /// <summary>The session's Ringo game, 404 when there is none. Host, admin or a player; 403 for anyone else.</summary>
    [HttpGet]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _ringoService.GetAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Anyone who unlocked the session: whether the Ringo game is open to join and whether the caller did.</summary>
    [HttpGet("lobby")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoLobbyDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLobby(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _ringoService.GetLobbyAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: creates a Ringo game, 409 when the session already has one.</summary>
    [HttpPost]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _ringoService.StartAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Admin only: opens the Ringo game to guests (idempotent), 403 for the host.</summary>
    [HttpPost("open")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Open(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _ringoService.OpenAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Joins the open Ringo game as a player and remembers the player in a cookie. 400 for a bad name, 409 while the
    /// game is closed or the name is taken. The session host's name comes from their Spotify profile.
    /// </summary>
    [HttpPost("join")]
    [RequireSessionAccess]
    [EnableRateLimiting(SessionsController.JoinRateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Join(
        Guid sessionId, [FromBody] RingoJoinRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _ringoService.JoinAsync(sessionId, request.Name, await Caller(sessionId), cancellationToken);
        if (!result.IsError)
        {
            Cookies.GrantGamePlayer(Kind, sessionId, result.Value, HttpContext);
        }

        return result.Match(_ => NoContent(), Problem);
    }

    /// <summary>Host or admin: removes a player from the Ringo game, 404 for an unknown game or player.</summary>
    [HttpDelete("players/{playerId:guid}")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> KickPlayer(Guid sessionId, Guid playerId, CancellationToken cancellationToken)
    {
        var result = await _ringoService.KickAsync(sessionId, playerId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: sets the turn order; 409 when the ids are not exactly the players.</summary>
    [HttpPut("players/order")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReorderPlayers(
        Guid sessionId, [FromBody] RingoPlayersOrderRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _ringoService.ReorderAsync(
            sessionId, request.PlayerIds, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Flips a card (0-51), idempotent. Host and admin any time, a player on their turn; 409 otherwise.</summary>
    [HttpPost("cards/{cardId:int}/flip")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> FlipCard(Guid sessionId, int cardId, CancellationToken cancellationToken)
    {
        var result = await _ringoService.FlipAsync(sessionId, cardId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Continues past the pending card and passes the turn. Host, admin or the player whose turn it is.</summary>
    [HttpPost("continue")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Continue(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _ringoService.ContinueAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Host or admin: new rules for a flipped ace from the language model, in the <c>Accept-Language</c> language, or
    /// from a built-in list when it is unavailable, none of them among the previous rules in the body. 400 for an
    /// oversized list, 403 for guests.
    /// </summary>
    [HttpPost("rule-suggestions")]
    [RequireSessionAccess]
    [EnableRateLimiting(SuggestionsRateLimitPolicy)]
    [ProducesResponseType<GameSuggestionsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SuggestRules(
        Guid sessionId, [FromBody] RingoRuleSuggestionsRequestDto request, CancellationToken cancellationToken)
    {
        var caller = await Caller(sessionId);

        var result = await _ruleSuggestionService.SuggestAsync(
            sessionId, request.PreviousRules, caller.ProfileId, caller.IsAdmin, GetLanguage(), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: ends the session's Ringo game, 404 when there is none.</summary>
    [HttpDelete]
    [RequireSessionAccess]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> End(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _ringoService.EndAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }
}
