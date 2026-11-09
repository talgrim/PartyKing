using PartyKing.API.Cookies;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PartyKing.API.Metrics;
using PartyKing.Application.Accounts.Services;

namespace PartyKing.API.Authentication.Schemes;

public class AccountCookieAuthenticationHandler : AuthenticationHandler<CookieAuthenticationOptions>
{
    private readonly ISignedCookies _cookies;
    private readonly AuthorizationMetrics _metrics;
    private readonly IAccountService _accountService;

    public AccountCookieAuthenticationHandler(
        IOptionsMonitor<CookieAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISignedCookies cookies,
        IAccountService accountService,
        AuthorizationMetrics metrics)
        : base(options, logger, encoder)
    {
        _metrics = metrics;
        _cookies = cookies;
        _accountService = accountService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var result = await AuthenticateAsync();
        var durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        _metrics.RecordDuration(Scheme.Name, durationMs, result.Succeeded);

        return result;
    }

    private async Task<AuthenticateResult> AuthenticateAsync()
    {
        if (!_cookies.TryReadAccount(Request, out var accountId, out var securityStamp))
        {
            return AuthenticateResult.Fail("Account cookie is missing or invalid");
        }

        // An account deleted after login, or whose password changed or that signed out everywhere, must not keep
        // working until the cookie expires. The role is the one the account has now, not the one at login.
        var identity = await _accountService.ValidateAsync(accountId, securityStamp, Context.RequestAborted);

        if (identity is null)
        {
            return AuthenticateResult.Fail("Account no longer exists or was signed out");
        }

        var claims = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, identity.Id.ToString()),
                new Claim(ClaimTypes.Role, identity.Role.ToString())
            ],
            nameof(AccountCookieAuthenticationHandler));

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(claims), Scheme.Name));
    }
}
