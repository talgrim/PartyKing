using PartyKing.API.Cookies;
using PartyKing.API.Authentication;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.API.Controllers.Games;

/// <summary>Base of the games controllers: who calls, as host and admin, and optionally as player of a game.</summary>
public abstract class GameControllerBase : CoreController
{
    /// <summary>Route of the games of a session; every game controller adds its own segment, e.g. <c>/ringo</c>.</summary>
    public const string GamesRoute = "sessions/{sessionId:guid}/games";

    public const string SuggestionsRateLimitPolicy = "game-suggestions";

    protected GameControllerBase(
        IHttpContextAccessor httpContextAccessor, ISignedCookies cookies)
        : base(httpContextAccessor)
    {
        Cookies = cookies;
    }

    protected ISignedCookies Cookies { get; }

    /// <summary>The caller as host and admin; with <paramref name="game"/> also as that game's player (id and seat stamp of the cookie).</summary>
    protected async Task<GameCaller> ReadCallerAsync(Guid sessionId, GameKind? game = null)
    {
        var session = await HttpContext.ReadSessionCallerAsync(Cookies);

        if (game is { } kind && Cookies.TryReadGamePlayer(kind, Request, sessionId, out var seat))
        {
            return new GameCaller(session.ProfileId, session.IsAdmin, seat.PlayerId, seat.Stamp);
        }

        return new GameCaller(session.ProfileId, session.IsAdmin);
    }
}
