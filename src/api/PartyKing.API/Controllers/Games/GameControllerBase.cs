using Microsoft.AspNetCore.Authentication;
using PartyKing.Application.Games.Services;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Authentication;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Controllers.Games;

/// <summary>Base of the controllers of games with a lobby: who calls, as host, admin and player of the game.</summary>
public abstract class GameControllerBase : CoreController
{
    /// <summary>Route of the games of a session; every game controller adds its own segment, e.g. <c>/ringo</c>.</summary>
    public const string GamesRoute = "sessions/{sessionId:guid}/games";

    public const string SuggestionsRateLimitPolicy = "game-suggestions";

    protected GameControllerBase(
        IHttpContextAccessor httpContextAccessor, ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor)
    {
        Cookies = cookieAuthorizationHandler;
    }

    protected ICookieAuthorizationHandler Cookies { get; }

    protected async Task<GameCaller> ReadCallerAsync(GameKind game, Guid sessionId)
    {
        var profileId = Cookies.TryReadProfileId(Request, out var id) ? id : null;
        var isAdmin = (await HttpContext.AuthenticateAsync(Constants.AdminScheme)).Succeeded;
        var playerId = Cookies.TryReadGamePlayer(game, Request, sessionId, out var player) ? player : (Guid?)null;

        return new GameCaller(profileId, isAdmin, playerId);
    }
}
