using PartyKing.API.Authentication;
using PartyKing.Application.Games.Services;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Controllers.Games;

/// <summary>Base of the games controllers: who calls, as host and admin, and optionally as player of a game.</summary>
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

    /// <summary>The caller as host and admin; with <paramref name="game"/> also as that game's player.</summary>
    protected async Task<GameCaller> ReadCallerAsync(Guid sessionId, GameKind? game = null)
    {
        var session = await HttpContext.ReadSessionCallerAsync(Cookies);

        Guid? playerId = null;
        if (game is { } kind && Cookies.TryReadGamePlayer(kind, Request, sessionId, out var player))
        {
            playerId = player;
        }

        return new GameCaller(session.ProfileId, session.IsAdmin, playerId);
    }
}
