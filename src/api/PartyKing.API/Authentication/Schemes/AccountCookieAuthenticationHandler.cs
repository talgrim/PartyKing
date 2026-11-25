using System.Diagnostics;
using System.Text.Encodings.Web;
using Identity.Client;
using Identity.Client.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PartyKing.API.Metrics;

namespace PartyKing.API.Authentication.Schemes;

/// <summary>
/// Signs in the account the identity service says owns the session cookie. The cookie is the identity service's, not
/// PartyKing's: <see cref="IdentityAuthenticationHandler"/> of the client package only forwards it (to
/// <see cref="IIdentityClient"/>, which caches the answer for a short time). A request without the cookie gets no result
/// and costs no call. When the identity service cannot be reached the request is answered 503, not 401: the UI reads a
/// 401 as "signed out" and would drop the hint. This adds only how long the authentication took.
/// </summary>
public class AccountCookieAuthenticationHandler : IdentityAuthenticationHandler
{
    private readonly AuthorizationMetrics _metrics;

    public AccountCookieAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IIdentityClient identityClient,
        IOptions<IdentityClientOptions> identityOptions,
        AuthorizationMetrics metrics)
        : base(options, logger, encoder, identityClient, identityOptions)
    {
        _metrics = metrics;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var result = await base.HandleAuthenticateAsync();
        var durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

        // A request without the cookie asked nobody, so it says nothing about how fast authentication is
        if (result.None)
        {
            return result;
        }

        _metrics.RecordDuration(Scheme.Name, durationMs, result.Succeeded);

        return result;
    }
}
