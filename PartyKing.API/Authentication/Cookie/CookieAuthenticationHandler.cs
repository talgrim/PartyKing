using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PartyKing.Application.Spotify.Services;
using PartyKing.Infrastructure.Authentication;

namespace PartyKing.API.Authentication.Cookie;

public class CookieAuthenticationHandler : AuthenticationHandler<CookieAuthenticationOptions>
{
    private readonly ISpotifyService _spotifyService;

    public CookieAuthenticationHandler(
        IOptionsMonitor<CookieAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISpotifyService spotifyService)
        : base(options, logger, encoder)
    {
        _spotifyService = spotifyService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(Constants.AuthCookieName, out var spotifyAccessToken))
        {
            if (!Request.Cookies.TryGetValue(Constants.UserCookieName, out var profileIdString))
            {
                return AuthenticateResult.Fail("Authentication cookie is missing");   
            }

            if (!int.TryParse(profileIdString, out var profileId))
            {
                return AuthenticateResult.Fail("Invalid User ID. Expected int");
            }

            var result = await _spotifyService.AuthenticateByProfileIdAsync(profileId, CancellationToken.None);

            if (result.IsError)
            {
                return AuthenticateResult.Fail(result.FirstError.Description);
            }

            spotifyAccessToken = result.Value;
        }

        var claims = CreateClaims(spotifyAccessToken);

        var cookieClaimsIdentity = new ClaimsIdentity(claims, nameof(CookieAuthenticationHandler));
        var customTicket = new AuthenticationTicket(new ClaimsPrincipal(cookieClaimsIdentity), Scheme.Name);
        return AuthenticateResult.Success(customTicket);
    }

    private static IEnumerable<Claim> CreateClaims(string spotifyAccessToken)
    {
        return
        [
            new Claim(ClaimTypes.Authentication, spotifyAccessToken)
        ];
    }
}