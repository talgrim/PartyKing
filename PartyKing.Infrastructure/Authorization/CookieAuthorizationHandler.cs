using Microsoft.AspNetCore.Http;
using PartyKing.Infrastructure.Authentication;

namespace PartyKing.Infrastructure.Authorization;

public interface ICookieAuthorizationHandler
{
    void Authorize(string profileId, HttpContext? httpContext);
    void Authorize(string spotifyAccessToken, int expiresIn, HttpContext? httpContext);
}

internal class CookieAuthorizationHandler : ICookieAuthorizationHandler
{
    private static readonly int UserExpirationTime = (int)TimeSpan.FromDays(30).TotalSeconds;

    public void Authorize(string profileId, HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            throw new InvalidOperationException("Null HTTP context when trying to authorize");
        }

        httpContext.Response.Cookies.Append(Constants.UserCookieName, profileId, GetOptions(UserExpirationTime));
    }

    public void Authorize(string spotifyAccessToken, int expiresIn, HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            throw new InvalidOperationException("Null HTTP context when trying to authorize");
        }

        httpContext.Response.Cookies.Append(Constants.AuthCookieName, spotifyAccessToken, GetOptions(expiresIn));
    }

    private static CookieOptions GetOptions(int expirationSeconds)
    {
        return new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            Expires = DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(expirationSeconds))
        };
    }
}