using Microsoft.AspNetCore.Authentication;
using PartyKing.API.Cookies;
using PartyKing.Application.Sessions;
using PartyKing.Contract.V1.Enums;

namespace PartyKing.API.Authentication;

public static class HttpContextCallerExtensions
{
    /// <summary>
    /// Who makes the request: the host from the signed host cookie, if any (whether their Spotify token still works
    /// is a separate matter, handled where it is used), and whether the identity service says the session cookie
    /// belongs to an admin (a signed-in user is not one).
    /// <para>
    /// This runs on the polled session endpoints (every game fetch among them). A guest has no session cookie, so the
    /// account scheme answers without asking anybody; a signed-in account is answered from the identity client's
    /// cache for a short time. While the identity service is unreachable a caller it has not answered for lately is
    /// a plain guest here rather than a failed request.
    /// </para>
    /// </summary>
    public static async Task<SessionCaller> ReadSessionCallerAsync(this HttpContext http, ISignedCookies cookies)
    {
        var profileId = cookies.TryReadProfileId(http.Request, out var id) ? id : null;
        var isAdmin = await IsAdminAccountAsync(http);

        return new SessionCaller(profileId, isAdmin);
    }

    /// <summary>The identity service knows the session cookie and the account is an admin now.</summary>
    private static async Task<bool> IsAdminAccountAsync(HttpContext http)
    {
        var account = await http.AuthenticateAsync(SchemeNames.Account);

        return account.Succeeded && account.Principal.IsInRole(nameof(AccountRole.Admin));
    }
}
