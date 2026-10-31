using PartyKing.Application.Games.Services;
using PartyKing.Domain.Entities.Games;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Controllers.Games.Resistance;

/// <summary>Base of the Resistance controllers, which all share the <c>games/resistance</c> route.</summary>
public abstract class ResistanceControllerBase : LobbyGameControllerBase
{
    protected const string ResistanceRoute = GamesRoute + "/resistance";

    protected ResistanceControllerBase(
        IHttpContextAccessor httpContextAccessor,
        ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(httpContextAccessor, cookieAuthorizationHandler)
    {
    }

    protected override GameKind Kind => GameKind.Resistance;
}
