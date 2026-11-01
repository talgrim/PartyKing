using PartyKing.Application.Ports;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Domain.Entities.Games;

namespace PartyKing.API.Controllers.Games;

/// <summary>Base of the controllers of games with a lobby, whose players are remembered in a cookie per game.</summary>
public abstract class LobbyGameControllerBase : GameControllerBase
{
    protected LobbyGameControllerBase(
        IHttpContextAccessor httpContextAccessor, ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor, cookieAuthorizationHandler)
    {
    }

    protected abstract GameKind Kind { get; }

    /// <summary>The caller as host, admin and player of this controller's game.</summary>
    protected Task<GameCaller> Caller(Guid sessionId) => ReadCallerAsync(sessionId, Kind);
}
