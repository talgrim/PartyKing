using Microsoft.AspNetCore.Authentication;
using PartyKing.API.Cookies;
using PartyKing.Application.Sessions;

namespace PartyKing.API.Authentication;

public static class HttpContextCallerExtensions
{
    /// <summary>
    /// Who makes the request: the host from the signed host cookie, if any (whether their Spotify token still works
    /// is a separate matter, handled where it is used), and whether the admin cookie authenticates.
    /// </summary>
    public static async Task<SessionCaller> ReadSessionCallerAsync(this HttpContext http, ISignedCookies cookies)
    {
        var profileId = cookies.TryReadProfileId(http.Request, out var id) ? id : null;
        var isAdmin = (await http.AuthenticateAsync(SchemeNames.Admin)).Succeeded;

        return new SessionCaller(profileId, isAdmin);
    }
}
