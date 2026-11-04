using Microsoft.AspNetCore.DataProtection;
using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.API.Cookies;

internal class SignedCookies : ISignedCookies
{
    private static readonly int UserExpirationTime = (int)TimeSpan.FromDays(30).TotalSeconds;
    private static readonly int AdminExpirationTime = (int)TimeSpan.FromHours(12).TotalSeconds;
    private static readonly int SessionAccessExpirationTime = (int)TimeSpan.FromDays(30).TotalSeconds;

    // Keeps the cookie small; when exceeded the oldest unlocks are forgotten and the password is asked again
    private const int MaxUnlockedSessions = 20;

    private readonly IDataProtector _userProtector;
    private readonly IDataProtector _adminProtector;
    private readonly IDataProtector _sessionAccessProtector;
    private readonly Dictionary<GameKind, (string CookieName, IDataProtector Protector)> _gamePlayerCookies;

    public SignedCookies(IDataProtectionProvider dataProtectionProvider)
    {
        _userProtector = dataProtectionProvider.CreateProtector("PartyKing.UserCookie");
        _adminProtector = dataProtectionProvider.CreateProtector("PartyKing.AdminCookie");
        _sessionAccessProtector = dataProtectionProvider.CreateProtector("PartyKing.SessionAccessCookie");
        _gamePlayerCookies = new()
        {
            [GameKind.Ringo] = (
                CookieNames.RingoPlayer, dataProtectionProvider.CreateProtector("PartyKing.RingoPlayerCookie")),
            [GameKind.Resistance] = (
                CookieNames.ResistancePlayer,
                dataProtectionProvider.CreateProtector("PartyKing.ResistancePlayerCookie")),
        };
    }

    public void GrantHost(string profileId, HttpContext context)
    {
        // Signed, so a client can't claim to be another host by editing the cookie
        context.Response.Cookies.Append(
            CookieNames.User,
            _userProtector.Protect(profileId),
            GetOptions(UserExpirationTime, context));
    }

    public void GrantSpotifyAccessToken(string spotifyAccessToken, int expiresIn, HttpContext context)
    {
        context.Response.Cookies.Append(CookieNames.Auth, spotifyAccessToken, GetOptions(expiresIn, context));
    }

    public void GrantAdmin(Guid adminId, HttpContext context)
    {
        context.Response.Cookies.Append(
            CookieNames.Admin,
            _adminProtector.Protect(adminId.ToString()),
            GetOptions(AdminExpirationTime, context));
    }

    public bool TryReadProfileId(HttpRequest request, out string profileId)
    {
        profileId = string.Empty;

        if (!request.Cookies.TryGetValue(CookieNames.User, out var protectedValue))
        {
            return false;
        }

        return TryUnprotect(_userProtector, protectedValue, out profileId);
    }

    public bool TryReadAdminId(HttpRequest request, out Guid adminId)
    {
        adminId = Guid.Empty;

        if (!request.Cookies.TryGetValue(CookieNames.Admin, out var protectedValue))
        {
            return false;
        }

        return TryUnprotect(_adminProtector, protectedValue, out var value) && Guid.TryParse(value, out adminId);
    }

    public void RevokeAdmin(HttpContext context)
    {
        context.Response.Cookies.Delete(CookieNames.Admin, new CookieOptions { Path = "/" });
    }

    public void GrantSessionAccess(Guid sessionId, string stamp, HttpContext context)
    {
        var unlocked = ReadSessionAccess(context.Request)
            .Where(x => x.SessionId != sessionId)
            .Append((SessionId: sessionId, Stamp: stamp))
            .TakeLast(MaxUnlockedSessions);

        context.Response.Cookies.Append(
            CookieNames.SessionAccess,
            _sessionAccessProtector.Protect(string.Join(';', unlocked.Select(x => $"{x.SessionId:N}:{x.Stamp}"))),
            GetOptions(SessionAccessExpirationTime, context));
    }

    public bool TryReadSessionAccess(HttpRequest request, Guid sessionId, out string stamp)
    {
        stamp = ReadSessionAccess(request).LastOrDefault(x => x.SessionId == sessionId).Stamp ?? string.Empty;

        return stamp.Length > 0;
    }

    public void GrantGamePlayer(GameKind game, Guid sessionId, Guid playerId, HttpContext context)
    {
        var (cookieName, protector) = _gamePlayerCookies[game];

        var players = ReadGamePlayers(game, context.Request)
            .Where(x => x.SessionId != sessionId)
            .Append((SessionId: sessionId, PlayerId: playerId))
            .TakeLast(MaxUnlockedSessions);

        context.Response.Cookies.Append(
            cookieName,
            protector.Protect(string.Join(';', players.Select(x => $"{x.SessionId:N}:{x.PlayerId:N}"))),
            GetOptions(SessionAccessExpirationTime, context));
    }

    public bool TryReadGamePlayer(GameKind game, HttpRequest request, Guid sessionId, out Guid playerId)
    {
        playerId = ReadGamePlayers(game, request).LastOrDefault(x => x.SessionId == sessionId).PlayerId;

        return playerId != Guid.Empty;
    }

    private IEnumerable<(Guid SessionId, Guid PlayerId)> ReadGamePlayers(GameKind game, HttpRequest request)
    {
        var (cookieName, protector) = _gamePlayerCookies[game];
        if (!request.Cookies.TryGetValue(cookieName, out var protectedValue)
            || !TryUnprotect(protector, protectedValue, out var value))
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
        if (!request.Cookies.TryGetValue(CookieNames.SessionAccess, out var protectedValue)
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
