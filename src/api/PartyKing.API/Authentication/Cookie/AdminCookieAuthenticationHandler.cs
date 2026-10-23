using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PartyKing.Application.Admin.Services;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Authentication.Cookie;

public class AdminCookieAuthenticationHandler : AuthenticationHandler<CookieAuthenticationOptions>
{
    private readonly ICookieAuthorizationHandler _cookieAuthorizationHandler;
    private readonly IAdminService _adminService;

    public AdminCookieAuthenticationHandler(
        IOptionsMonitor<CookieAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ICookieAuthorizationHandler cookieAuthorizationHandler,
        IAdminService adminService)
        : base(options, logger, encoder)
    {
        _cookieAuthorizationHandler = cookieAuthorizationHandler;
        _adminService = adminService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_cookieAuthorizationHandler.TryReadAdminId(Request, out var adminId))
        {
            return AuthenticateResult.Fail("Admin cookie is missing or invalid");
        }

        // An account deleted after login must not keep working until the cookie expires
        if (!await _adminService.ExistsAsync(adminId, Context.RequestAborted))
        {
            return AuthenticateResult.Fail("Admin account no longer exists");
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, adminId.ToString())],
            nameof(AdminCookieAuthenticationHandler));

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
