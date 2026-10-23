using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PartyKing.Application.Spotify.Services;
using PartyKing.Infrastructure.Authentication;
using PartyKing.Infrastructure.Authorization;

namespace PartyKing.API.Authentication.Cookie;

public class CookieAuthenticationHandler : AuthenticationHandler<CookieAuthenticationOptions>
{
    private readonly ISpotifyService _spotifyService;
    private readonly ICookieAuthorizationHandler _cookieAuthorizationHandler;

    public CookieAuthenticationHandler(
        IOptionsMonitor<CookieAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISpotifyService spotifyService,
        ICookieAuthorizationHandler cookieAuthorizationHandler)
        : base(options, logger, encoder)
    {
        _spotifyService = spotifyService;
        _cookieAuthorizationHandler = cookieAuthorizationHandler;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // The profile id comes from the signed cookie, never from anything the client can edit
        if (!_cookieAuthorizationHandler.TryReadProfileId(Request, out var profileId))
        {
            return AuthenticateResult.Fail("Authentication cookie is missing or invalid");
        }

        if (!Request.Cookies.TryGetValue(Constants.AuthCookieName, out var spotifyAccessToken))
        {
            var result = await _spotifyService.AuthenticateByProfileIdAsync(profileId, CancellationToken.None);

            if (result.IsError)
            {
                return AuthenticateResult.Fail(result.FirstError.Description);
            }

            spotifyAccessToken = result.Value;
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