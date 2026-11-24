using System.Diagnostics;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PartyKing.API.Metrics;
using PartyKing.Application.Configuration;
using PartyKing.Application.Ports;

namespace PartyKing.API.Authentication.Schemes;

/// <summary>
/// Signs in the account the identity service says owns the session cookie. The cookie is the identity service's, not
/// PartyKing's: this only forwards it (<see cref="IIdentityClient"/>, which caches the answer for a short time). A
/// request without the cookie gets no result and costs no call. When the identity service cannot be reached the request
/// is answered 503, not 401: the UI reads a 401 as "signed out" and would drop the hint.
/// </summary>
public class AccountCookieAuthenticationHandler : AuthenticationHandler<CookieAuthenticationOptions>
{
    private const string ResultItemKey = "PartyKing.Identity.Result";

    private readonly IIdentityClient _identityClient;
    private readonly IOptions<IdentityConfiguration> _identityOptions;
    private readonly AuthorizationMetrics _metrics;
    private bool _identityUnavailable;

    public AccountCookieAuthenticationHandler(
        IOptionsMonitor<CookieAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IIdentityClient identityClient,
        IOptions<IdentityConfiguration> identityOptions,
        AuthorizationMetrics metrics)
        : base(options, logger, encoder)
    {
        _identityClient = identityClient;
        _identityOptions = identityOptions;
        _metrics = metrics;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var result = await AuthenticateAsync();
        var durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

        // A request without the cookie asked nobody, so it says nothing about how fast authentication is
        if (result.None)
        {
            return result;
        }

        _metrics.RecordDuration(Scheme.Name, durationMs, result.Succeeded);

        return result;
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = _identityUnavailable
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status401Unauthorized;

        return Task.CompletedTask;
    }

    private async Task<AuthenticateResult> AuthenticateAsync()
    {
        // Guests have the host's cookies at most: without the identity service's cookie there is nobody to ask about
        if (!Request.Cookies.ContainsKey(_identityOptions.Value.CookieName))
        {
            return AuthenticateResult.NoResult();
        }

        var answer = await AskAsync(Request.Headers.Cookie.ToString());

        switch (answer)
        {
            case IdentityResult.Account account:
                return AuthenticateResult.Success(new AuthenticationTicket(ToPrincipal(account), Scheme.Name));
            case IdentityResult.Unavailable:
                _identityUnavailable = true;

                return AuthenticateResult.Fail("The identity service is unavailable");
            default:
                return AuthenticateResult.Fail("Session is missing, expired or signed out");
        }
    }

    // The same request can ask more than once (a guard and then the caller of a polled endpoint): one answer is enough
    private async Task<IdentityResult> AskAsync(string cookieHeader)
    {
        if (Context.Items.TryGetValue(ResultItemKey, out var known) && known is IdentityResult cached)
        {
            return cached;
        }

        var answer = await _identityClient.GetCallerAsync(cookieHeader, null, Context.RequestAborted);

        Context.Items[ResultItemKey] = answer;

        return answer;
    }

    private ClaimsPrincipal ToPrincipal(IdentityResult.Account account)
    {
        var claims = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
                new Claim(ClaimTypes.Name, account.Username),
                new Claim(ClaimTypes.Role, account.Role.ToString())
            ],
            nameof(AccountCookieAuthenticationHandler));

        return new ClaimsPrincipal(claims);
    }
}
