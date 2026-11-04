using PartyKing.API.Cookies;
using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.API.Controllers.Games.Resistance;

/// <summary>Base of the Resistance controllers, which all share the <c>games/resistance</c> route.</summary>
public abstract class ResistanceControllerBase : LobbyGameControllerBase
{
    protected const string ResistanceRoute = GamesRoute + "/resistance";

    protected ResistanceControllerBase(
        IHttpContextAccessor httpContextAccessor,
        ISignedCookies cookies)
        : base(httpContextAccessor, cookies)
    {
    }

    protected override GameKind Kind => GameKind.Resistance;
}
