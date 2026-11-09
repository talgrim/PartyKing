using Microsoft.AspNetCore.Authorization;
using PartyKing.API.Authentication;

namespace PartyKing.API.Authorization;

/// <summary>Any signed-in account, user or admin.</summary>
public class AuthorizeAccountAttribute : AuthorizeAttribute
{
    public AuthorizeAccountAttribute() : base(Policies.Account)
    {
        AuthenticationSchemes = SchemeNames.Account;
    }
}
