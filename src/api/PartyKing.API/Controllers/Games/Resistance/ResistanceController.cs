using PartyKing.Application.Ports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PartyKing.API.Authorization;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Contract.V1.Games.Resistance;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Controllers.Games.Resistance;

/// <summary>The lifecycle of a session's Resistance game: lobby, rules, start, players and end.</summary>
[Route(ResistanceRoute)]
public class ResistanceController : ResistanceControllerBase
{
    public ResistanceController(
        IHttpContextAccessor httpContextAccessor,
        IResistanceLobbyService lobbyService,
        IResistanceRoundService roundService,
        ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor, cookieAuthorizationHandler)
    {
        Lobby = lobbyService;
        Rounds = roundService;
    }

    private IResistanceLobbyService Lobby { get; }

    private IResistanceRoundService Rounds { get; }

    /// <summary>
    /// The session's Resistance game. Anyone who unlocked the session may watch it once it is open; until then it is
    /// 404 for everyone but the host and admins, and 404 when there is no game.
    /// </summary>
    [HttpGet]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await Lobby.GetAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Anyone who unlocked the session: whether the Resistance game is open to join and whether the caller did.</summary>
    [HttpGet("lobby")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceLobbyDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLobby(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await Lobby.GetLobbyAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: creates the Resistance game (closed to guests), 409 when the session already has one.</summary>
    [HttpPost]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await Lobby.CreateAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Host or admin: replaces the game's rules (spies, team sizes, fails needed) with the current server
    /// configuration. 409 once the game is open or started.
    /// </summary>
    [HttpPost("rules/refresh")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshRules(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await Lobby.RefreshRulesAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Admin only: opens the Resistance game to guests (idempotent), 403 for the host.</summary>
    [HttpPost("open")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Open(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await Lobby.OpenAsync(sessionId, await Caller(sessionId), cancellationToken);

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
    public async Task<IActionResult> Join(
        Guid sessionId, [FromBody] ResistanceJoinRequestDto request, CancellationToken cancellationToken)
    {
        var result = await Lobby.JoinAsync(
            sessionId, request.Name, await Caller(sessionId), cancellationToken);
        if (!result.IsError)
        {
            Cookies.GrantGamePlayer(Kind, sessionId, result.Value, HttpContext);
        }

        return result.Match(_ => NoContent(), Problem);
    }

    /// <summary>
    /// Host or admin: starts the open Resistance game (deals the roles, picks the first leader, locks the players).
    /// 409 while it is closed or already started or has fewer than 5 players.
    /// </summary>
    [HttpPost("start")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Start(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await Rounds.StartAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>
    /// Host or admin: starts a finished Resistance game over with the same players (new roles and leader, mission 1).
    /// 409 until the game is over.
    /// </summary>
    [HttpPost("restart")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Restart(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await Rounds.RestartAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: removes a player from the Resistance game, 404 for an unknown game or player.</summary>
    [HttpDelete("players/{playerId:guid}")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> KickPlayer(Guid sessionId, Guid playerId, CancellationToken cancellationToken)
    {
        var result = await Lobby.KickAsync(sessionId, playerId, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: sets the seating order; 409 when the ids are not exactly the players.</summary>
    [HttpPut("players/order")]
    [RequireSessionAccess]
    [ProducesResponseType<ResistanceGameDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReorderPlayers(
        Guid sessionId, [FromBody] ResistancePlayersOrderRequestDto request, CancellationToken cancellationToken)
    {
        var result = await Lobby.ReorderAsync(
            sessionId, request.PlayerIds, await Caller(sessionId), cancellationToken);

        return result.Match(Ok, Problem);
    }

    /// <summary>Host or admin: ends the Resistance game, 404 when there is none.</summary>
    [HttpDelete]
    [RequireSessionAccess]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> End(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await Lobby.EndAsync(sessionId, await Caller(sessionId), cancellationToken);

        return result.Match(_ => NoContent(), Problem);
    }
}
