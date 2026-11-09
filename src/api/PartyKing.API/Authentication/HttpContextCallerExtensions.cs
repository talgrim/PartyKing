using Microsoft.AspNetCore.Authentication;
using PartyKing.Domain.Entities.Auth;
using PartyKing.API.Cookies;
using PartyKing.Application.Sessions;

namespace PartyKing.API.Authentication;

public static class HttpContextCallerExtensions
{
    /// <summary>
    /// Who makes the request: the host from the signed host cookie, if any (whether their Spotify token still works
    /// is a separate matter, handled where it is used), and whether the account cookie authenticates an admin (a
    /// signed-in user is not one).
    /// </summary>
    public static async Task<SessionCaller> ReadSessionCallerAsync(this HttpContext http, ISignedCookies cookies)
    {
        var profileId = cookies.TryReadProfileId(http.Request, out var id) ? id : null;
        var account = await http.AuthenticateAsync(SchemeNames.Account);
        var isAdmin = account.Succeeded && account.Principal.IsInRole(nameof(AccountRole.Admin));

        return new SessionCaller(profileId, isAdmin);
    }
}
