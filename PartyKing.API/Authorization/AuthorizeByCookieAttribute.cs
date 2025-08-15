using Microsoft.AspNetCore.Authorization;
using PartyKing.API.Authentication;
using PartyKing.Infrastructure.Authentication;

namespace PartyKing.API.Authorization;

public class AuthorizeByCookieAttribute : AuthorizeAttribute
{
    public AuthorizeByCookieAttribute() : base(Policies.AuthenticatedUser)
    {
        AuthenticationSchemes = Constants.Scheme;
    }
}
