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

    /// <summary>Remembers that this browser entered the session's password; <paramref name="stamp"/> ties it to that password.</summary>
    void GrantSessionAccess(Guid sessionId, string stamp, HttpContext? httpContext);

    /// <summary>Reads the stamp stored for the session; false when there is none or the cookie was tampered with.</summary>
    bool TryReadSessionAccess(HttpRequest request, Guid sessionId, out string stamp);

    /// <summary>Remembers the Ringo player this browser joined as in the session.</summary>
    void GrantRingoPlayer(Guid sessionId, Guid playerId, HttpContext? httpContext);

    /// <summary>Reads the Ringo player id stored for the session; false when there is none or the cookie was tampered with.</summary>
    bool TryReadRingoPlayer(HttpRequest request, Guid sessionId, out Guid playerId);
}

internal class CookieAuthorizationHandler : ICookieAuthorizationHandler
{
    private static readonly int UserExpirationTime = (int)TimeSpan.FromDays(30).TotalSeconds;
    private static readonly int AdminExpirationTime = (int)TimeSpan.FromHours(12).TotalSeconds;
    private static readonly int SessionAccessExpirationTime = (int)TimeSpan.FromDays(30).TotalSeconds;

    // Keeps the cookie small; when exceeded the oldest unlocks are forgotten and the password is asked again
    private const int MaxUnlockedSessions = 20;

    private readonly IDataProtector _userProtector;
    private readonly IDataProtector _adminProtector;
    private readonly IDataProtector _sessionAccessProtector;
    private readonly IDataProtector _ringoPlayerProtector;

    public CookieAuthorizationHandler(IDataProtectionProvider dataProtectionProvider)
    {
        _userProtector = dataProtectionProvider.CreateProtector("PartyKing.UserCookie");
        _adminProtector = dataProtectionProvider.CreateProtector("PartyKing.AdminCookie");
        _sessionAccessProtector = dataProtectionProvider.CreateProtector("PartyKing.SessionAccessCookie");
        _ringoPlayerProtector = dataProtectionProvider.CreateProtector("PartyKing.RingoPlayerCookie");
    }

    public void Authorize(string profileId, HttpContext? httpContext)
    {
        var context = Require(httpContext);

        // Signed, so a client can't claim to be another host by editing the cookie
        context.Response.Cookies.Append(
            Constants.UserCookieName,
            _userProtector.Protect(profileId),
            GetOptions(UserExpirationTime, context));
    }

    public void Authorize(string spotifyAccessToken, int expiresIn, HttpContext? httpContext)
    {
        var context = Require(httpContext);

        context.Response.Cookies.Append(Constants.AuthCookieName, spotifyAccessToken, GetOptions(expiresIn, context));
    }

    public void AuthorizeAdmin(Guid adminId, HttpContext? httpContext)
    {
        var context = Require(httpContext);

        context.Response.Cookies.Append(
            Constants.AdminCookieName,
            _adminProtector.Protect(adminId.ToString()),
            GetOptions(AdminExpirationTime, context));
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

    public void GrantSessionAccess(Guid sessionId, string stamp, HttpContext? httpContext)
    {
        var context = Require(httpContext);

        var unlocked = ReadSessionAccess(context.Request)
            .Where(x => x.SessionId != sessionId)
            .Append((SessionId: sessionId, Stamp: stamp))
            .TakeLast(MaxUnlockedSessions);

        context.Response.Cookies.Append(
            Constants.SessionAccessCookieName,
            _sessionAccessProtector.Protect(string.Join(';', unlocked.Select(x => $"{x.SessionId:N}:{x.Stamp}"))),
            GetOptions(SessionAccessExpirationTime, context));
    }

    public bool TryReadSessionAccess(HttpRequest request, Guid sessionId, out string stamp)
    {
        stamp = ReadSessionAccess(request).LastOrDefault(x => x.SessionId == sessionId).Stamp ?? string.Empty;

        return stamp.Length > 0;
    }

    public void GrantRingoPlayer(Guid sessionId, Guid playerId, HttpContext? httpContext)
    {
        var context = Require(httpContext);

        var players = ReadRingoPlayers(context.Request)
            .Where(x => x.SessionId != sessionId)
            .Append((SessionId: sessionId, PlayerId: playerId))
            .TakeLast(MaxUnlockedSessions);

        context.Response.Cookies.Append(
            Constants.RingoPlayerCookieName,
            _ringoPlayerProtector.Protect(string.Join(';', players.Select(x => $"{x.SessionId:N}:{x.PlayerId:N}"))),
            GetOptions(SessionAccessExpirationTime, context));
    }

    public bool TryReadRingoPlayer(HttpRequest request, Guid sessionId, out Guid playerId)
    {
        playerId = ReadRingoPlayers(request).LastOrDefault(x => x.SessionId == sessionId).PlayerId;

        return playerId != Guid.Empty;
    }

    private IEnumerable<(Guid SessionId, Guid PlayerId)> ReadRingoPlayers(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(Constants.RingoPlayerCookieName, out var protectedValue)
            || !TryUnprotect(_ringoPlayerProtector, protectedValue, out var value))
        {
            yield break;
        }

        foreach (var entry in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (entry.Split(':') is [var session, var player]
                && Guid.TryParseExact(session, "N", out var sessionId)
                && Guid.TryParseExact(player, "N", out var playerId))
            {
                yield return (sessionId, playerId);
            }
        }
    }

    private IEnumerable<(Guid SessionId, string Stamp)> ReadSessionAccess(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(Constants.SessionAccessCookieName, out var protectedValue)
            || !TryUnprotect(_sessionAccessProtector, protectedValue, out var value))
        {
            yield break;
        }

        foreach (var entry in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (entry.Split(':') is [var id, var stamp] && Guid.TryParseExact(id, "N", out var sessionId))
            {
                yield return (sessionId, stamp);
            }
        }
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

    private static CookieOptions GetOptions(int expirationSeconds, HttpContext context)
    {
        return new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(expirationSeconds))
        };
    }
}
