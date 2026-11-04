using PartyKing.API.Cookies;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PartyKing.API.Metrics;
using PartyKing.Application.Spotify.Services;

namespace PartyKing.API.Authentication.Schemes;

public class CookieAuthenticationHandler : AuthenticationHandler<CookieAuthenticationOptions>
{
    private readonly ISpotifyService _spotifyService;
    private readonly ISignedCookies _cookies;
    private readonly AuthorizationMetrics _metrics;

    public CookieAuthenticationHandler(
        IOptionsMonitor<CookieAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISpotifyService spotifyService,
        ISignedCookies cookies,
        AuthorizationMetrics metrics)
        : base(options, logger, encoder)
    {
        _metrics = metrics;
        _spotifyService = spotifyService;
        _cookies = cookies;
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
        // The profile id comes from the signed cookie, never from anything the client can edit
        if (!_cookies.TryReadProfileId(Request, out var profileId))
        {
            return AuthenticateResult.Fail("Authentication cookie is missing or invalid");
        }

        if (!Request.Cookies.TryGetValue(CookieNames.Auth, out var spotifyAccessToken))
        {
            var result = await _spotifyService.AuthenticateByProfileIdAsync(profileId, CancellationToken.None);

            if (result.IsError)
            {
                return AuthenticateResult.Fail(result.FirstError.Description);
            }

            _cookies.GrantSpotifyAccessToken(result.Value.AccessToken, result.Value.ExpiresIn, Context);
            spotifyAccessToken = result.Value.AccessToken;
        }

        var claims = CreateClaims(profileId, spotifyAccessToken);

        var cookieClaimsIdentity = new ClaimsIdentity(claims, nameof(CookieAuthenticationHandler));
        var customTicket = new AuthenticationTicket(new ClaimsPrincipal(cookieClaimsIdentity), Scheme.Name);
        return AuthenticateResult.Success(customTicket);
    }

    private static IEnumerable<Claim> CreateClaims(string profileId, string spotifyAccessToken)
    {
        return
        [
            new Claim(ClaimTypes.NameIdentifier, profileId),
            new Claim(ClaimTypes.Authentication, spotifyAccessToken)
        ];
    }
}