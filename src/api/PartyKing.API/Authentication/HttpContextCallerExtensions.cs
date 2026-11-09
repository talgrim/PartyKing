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
    /// <para>
    /// This runs on the polled session endpoints (every game fetch among them) and every registered guest has an
    /// account cookie, so a user's cookie must cost no database read: the role is signed into the cookie, and only a
    /// cookie that says <see cref="AccountRole.Admin"/> is checked against the database. A user promoted to admin
    /// by editing the row is an admin here after the next login.
    /// </para>
    /// </summary>
    public static async Task<SessionCaller> ReadSessionCallerAsync(this HttpContext http, ISignedCookies cookies)
    {
        var profileId = cookies.TryReadProfileId(http.Request, out var id) ? id : null;
        var isAdmin = cookies.TryReadAccount(http.Request, out _, out _, out var cookieRole)
            && cookieRole == AccountRole.Admin
            && await IsAdminAccountAsync(http);

        return new SessionCaller(profileId, isAdmin);
    }

    /// <summary>The account still exists, its stamp matches and it is an admin now.</summary>
    private static async Task<bool> IsAdminAccountAsync(HttpContext http)
    {
        var account = await http.AuthenticateAsync(SchemeNames.Account);

        return account.Succeeded && account.Principal.IsInRole(nameof(AccountRole.Admin));
    }
}
