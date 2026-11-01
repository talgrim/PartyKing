using PartyKing.Application.Ports;
using Microsoft.AspNetCore.Authentication;
using PartyKing.Application.Sessions;
using PartyKing.Infrastructure.Authentication;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Authentication;

public static class HttpContextCallerExtensions
{
    /// <summary>
    /// Who makes the request: the host from the signed host cookie, if any (whether their Spotify token still works
    /// is a separate matter, handled where it is used), and whether the admin cookie authenticates.
    /// </summary>
    public static async Task<SessionCaller> ReadSessionCallerAsync(
        this HttpContext http, ICookieAuthorizationHandler cookies)
    {
        var profileId = cookies.TryReadProfileId(http.Request, out var id) ? id : null;
        var isAdmin = (await http.AuthenticateAsync(Constants.AdminScheme)).Succeeded;

        return new SessionCaller(profileId, isAdmin);
    }
}
