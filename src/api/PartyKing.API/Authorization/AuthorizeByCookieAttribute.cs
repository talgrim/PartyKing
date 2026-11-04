using Microsoft.AspNetCore.Authorization;
using PartyKing.API.Authentication;

namespace PartyKing.API.Authorization;

public class AuthorizeByCookieAttribute : AuthorizeAttribute
{
    public AuthorizeByCookieAttribute() : base(Policies.AuthenticatedUser)
    {
        AuthenticationSchemes = SchemeNames.Host;
    }
}
