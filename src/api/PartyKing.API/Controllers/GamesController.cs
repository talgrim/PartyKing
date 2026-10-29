using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PartyKing.API.Authorization;
using PartyKing.Application.Games.Services;
using PartyKing.Contract.V1.Games;
using PartyKing.Infrastructure.Authentication;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Controllers;

public class GamesController : CoreController
{
    public const string SuggestionsRateLimitPolicy = "game-suggestions";

    private readonly IGameService _gameService;
    private readonly IRingoService _ringoService;
    private readonly INeverHaveIEverSuggestionService _suggestionService;
    private readonly IRingoRuleSuggestionService _ruleSuggestionService;
    private readonly ICookieAuthorizationHandler _cookieAuthorizationHandler;

    public GamesController(
        IHttpContextAccessor httpContextAccessor,
        IGameService gameService,
        IRingoService ringoService,
        INeverHaveIEverSuggestionService suggestionService,
        IRingoRuleSuggestionService ruleSuggestionService,
        ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor)
    {
        _gameService = gameService;
        _ringoService = ringoService;
        _suggestionService = suggestionService;
        _ruleSuggestionService = ruleSuggestionService;
        _cookieAuthorizationHandler = cookieAuthorizationHandler;
    }

    /// <summary>Host or admin: the games available in the session. 403 for guests.</summary>
    [HttpGet("sessions/{sessionId:guid}/games")]
    [RequireSessionAccess]
    [ProducesResponseType<IReadOnlyList<GameDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGames(Guid sessionId, CancellationToken cancellationToken)
    {
        var profileId = _cookieAuthorizationHandler.TryReadProfileId(Request, out var id) ? id : null;
        var isAdmin = (await HttpContext.AuthenticateAsync(Constants.AdminScheme)).Succeeded;

        var result = await _gameService.ListAsync(sessionId, profileId, isAdmin, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Host or admin: spicy "Never have I ever" statements in Polish from the language model, or from a built-in list
    /// when it is unavailable. 403 for guests.
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/games/never-have-i-ever/suggestions")]
    [RequireSessionAccess]
    [EnableRateLimiting(SuggestionsRateLimitPolicy)]
    [ProducesResponseType<GameSuggestionsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNeverHaveIEverSuggestions(Guid sessionId, CancellationToken cancellationToken)
    {
        var profileId = _cookieAuthorizationHandler.TryReadProfileId(Request, out var id) ? id : null;
        var isAdmin = (await HttpContext.AuthenticateAsync(Constants.AdminScheme)).Succeeded;

        var result = await _suggestionService.SuggestAsync(sessionId, profileId, isAdmin, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>The session's Ringo game, 404 when there is none. Host, admin or a player; 403 for anyone else.</summary>
    [HttpGet("sessions/{sessionId:guid}/games/ringo")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRingo(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _ringoService.GetAsync(sessionId, await ReadCallerAsync(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Anyone who unlocked the session: whether the Ringo game is open to join and whether the caller did.</summary>
    [HttpGet("sessions/{sessionId:guid}/games/ringo/lobby")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoLobbyDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRingoLobby(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _ringoService.GetLobbyAsync(sessionId, await ReadCallerAsync(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: starts a Ringo game, 409 when the session already has one.</summary>
    [HttpPost("sessions/{sessionId:guid}/games/ringo")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> StartRingo(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _ringoService.StartAsync(sessionId, await ReadCallerAsync(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Admin only: opens the Ringo game to guests (idempotent), 403 for the host.</summary>
    [HttpPost("sessions/{sessionId:guid}/games/ringo/open")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> OpenRingo(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _ringoService.OpenAsync(sessionId, await ReadCallerAsync(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Joins the open Ringo game as a player and remembers the player in a cookie. 400 for a bad name, 409 while the
    /// game is closed or the name is taken. The session host's name comes from their Spotify profile.
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/games/ringo/join")]
    [RequireSessionAccess]
    [EnableRateLimiting(SessionsController.JoinRateLimitPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> JoinRingo(
        Guid sessionId, [FromBody] RingoJoinRequest request, CancellationToken cancellationToken)
    {
        var result = await _ringoService.JoinAsync(
            sessionId, request.Name, await ReadCallerAsync(sessionId), cancellationToken);
        if (!result.IsError)
        {
            _cookieAuthorizationHandler.GrantRingoPlayer(sessionId, result.Value, HttpContext);
        }

        return result.Match(_ => NoContent(), Problem);
    }

    /// <summary>Host or admin: removes a player from the Ringo game, 404 for an unknown game or player.</summary>
    [HttpDelete("sessions/{sessionId:guid}/games/ringo/players/{playerId:guid}")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> KickRingoPlayer(Guid sessionId, Guid playerId, CancellationToken cancellationToken)
    {
        var result = await _ringoService.KickAsync(
            sessionId, playerId, await ReadCallerAsync(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: sets the turn order; 409 when the ids are not exactly the players.</summary>
    [HttpPut("sessions/{sessionId:guid}/games/ringo/players/order")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReorderRingoPlayers(
        Guid sessionId, [FromBody] RingoPlayersOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _ringoService.ReorderAsync(
            sessionId, request.PlayerIds, await ReadCallerAsync(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Flips a card (0-51), idempotent. Host and admin any time, a player on their turn; 409 otherwise.</summary>
    [HttpPost("sessions/{sessionId:guid}/games/ringo/cards/{cardId:int}/flip")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> FlipRingoCard(Guid sessionId, int cardId, CancellationToken cancellationToken)
    {
        var result = await _ringoService.FlipAsync(
            sessionId, cardId, await ReadCallerAsync(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Continues past the pending card and passes the turn. Host, admin or the player whose turn it is.</summary>
    [HttpPost("sessions/{sessionId:guid}/games/ringo/continue")]
    [RequireSessionAccess]
    [ProducesResponseType<RingoGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ContinueRingo(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _ringoService.ContinueAsync(sessionId, await ReadCallerAsync(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Host or admin: new rules in Polish for a flipped ace from the language model, or from a built-in list when it is
    /// unavailable, none of them among the previous rules in the body. 400 for an oversized list, 403 for guests.
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/games/ringo/rule-suggestions")]
    [RequireSessionAccess]
    [EnableRateLimiting(SuggestionsRateLimitPolicy)]
    [ProducesResponseType<GameSuggestionsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRingoRuleSuggestions(
        Guid sessionId,
        [FromBody] RingoRuleSuggestionsRequest request,
        CancellationToken cancellationToken)
    {
        var caller = await ReadCallerAsync(sessionId);

        var result = await _ruleSuggestionService.SuggestAsync(
            sessionId, request.PreviousRules, caller.ProfileId, caller.IsAdmin, cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: ends the session's Ringo game, 404 when there is none.</summary>
    [HttpDelete("sessions/{sessionId:guid}/games/ringo")]
    [RequireSessionAccess]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> EndRingo(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _ringoService.EndAsync(sessionId, await ReadCallerAsync(sessionId), cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }

    private async Task<RingoCaller> ReadCallerAsync(Guid sessionId)
    {
        var profileId = _cookieAuthorizationHandler.TryReadProfileId(Request, out var id) ? id : null;
        var isAdmin = (await HttpContext.AuthenticateAsync(Constants.AdminScheme)).Succeeded;
        var playerId = _cookieAuthorizationHandler.TryReadRingoPlayer(Request, sessionId, out var player)
            ? player
            : (Guid?)null;

        return new RingoCaller(profileId, isAdmin, playerId);
    }
}
