using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using PartyKing.Infrastructure.Authentication;

namespace PartyKing.Infrastructure.Authorization;

public interface ICookieAuthorizationHandler
{
    void Authorize(string profileId, HttpContext? httpContext);
    void Authorize(string spotifyAccessToken, int expiresIn, HttpContext? httpContext);
    void AuthorizeAdmin(Guid adminId, HttpContext? httpContext);

    /// <summary>Reads the host's Spotify profile id; false when the cookie is missing or was tampered with.</summary>
    bool TryReadProfileId(HttpRequest request, out string profileId);

    /// <summary>Reads the admin account id; false when the cookie is missing or was tampered with.</summary>
    bool TryReadAdminId(HttpRequest request, out Guid adminId);
    void SignOutAdmin(HttpContext? httpContext);
}

internal class CookieAuthorizationHandler : ICookieAuthorizationHandler
{
    private static readonly int UserExpirationTime = (int)TimeSpan.FromDays(30).TotalSeconds;
    private static readonly int AdminExpirationTime = (int)TimeSpan.FromHours(12).TotalSeconds;

    private readonly IDataProtector _userProtector;
    private readonly IDataProtector _adminProtector;

    public CookieAuthorizationHandler(IDataProtectionProvider dataProtectionProvider)
    {
        _userProtector = dataProtectionProvider.CreateProtector("PartyKing.UserCookie");
        _adminProtector = dataProtectionProvider.CreateProtector("PartyKing.AdminCookie");
    }

    public void Authorize(string profileId, HttpContext? httpContext)
    {
        var context = Require(httpContext);

        // Signed, so a client can't claim to be another host by editing the cookie
        context.Response.Cookies.Append(
            Constants.UserCookieName,
            _userProtector.Protect(profileId),
            GetOptions(UserExpirationTime));
    }

    public void Authorize(string spotifyAccessToken, int expiresIn, HttpContext? httpContext)
    {
        var context = Require(httpContext);

        context.Response.Cookies.Append(Constants.AuthCookieName, spotifyAccessToken, GetOptions(expiresIn));
    }

    public void AuthorizeAdmin(Guid adminId, HttpContext? httpContext)
    {
        var context = Require(httpContext);

        context.Response.Cookies.Append(
            Constants.AdminCookieName,
            _adminProtector.Protect(adminId.ToString()),
            GetOptions(AdminExpirationTime));
    }

    public bool TryReadProfileId(HttpRequest request, out string profileId)
    {
        profileId = string.Empty;

        if (!request.Cookies.TryGetValue(Constants.UserCookieName, out var protectedValue))
        {
            return false;
        }

        return TryUnprotect(_userProtector, protectedValue, out profileId);
    }

    public bool TryReadAdminId(HttpRequest request, out Guid adminId)
    {
        adminId = Guid.Empty;

        if (!request.Cookies.TryGetValue(Constants.AdminCookieName, out var protectedValue))
        {
            return false;
        }

        return TryUnprotect(_adminProtector, protectedValue, out var value) && Guid.TryParse(value, out adminId);
    }

    public void SignOutAdmin(HttpContext? httpContext)
    {
        Require(httpContext).Response.Cookies.Delete(Constants.AdminCookieName, new CookieOptions { Path = "/" });
    }

    private static bool TryUnprotect(IDataProtector protector, string? protectedValue, out string value)
    {
        value = string.Empty;

        if (string.IsNullOrEmpty(protectedValue))
        {
            return false;
        }

        try
        {
            value = protector.Unprotect(protectedValue);
            return true;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }

    private static HttpContext Require(HttpContext? httpContext)
    {
        return httpContext ?? throw new InvalidOperationException("Null HTTP context when trying to authorize");
    }

    private static CookieOptions GetOptions(int expirationSeconds)
    {
        return new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(expirationSeconds))
        };
    }
}
