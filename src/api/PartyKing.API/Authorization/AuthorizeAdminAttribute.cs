using Microsoft.AspNetCore.Authorization;
using PartyKing.API.Authentication;
using PartyKing.Infrastructure.Authentication;

namespace PartyKing.API.Authorization;

public class AuthorizeAdminAttribute : AuthorizeAttribute
{
    public AuthorizeAdminAttribute() : base(Policies.Admin)
    {
        AuthenticationSchemes = Constants.AdminScheme;
    }
}
