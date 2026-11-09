using Microsoft.AspNetCore.DataProtection;
using PartyKing.Application.Accounts;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.API.Cookies;

internal class SignedCookies : ISignedCookies
{
    private static readonly int UserExpirationTime = (int)TimeSpan.FromDays(30).TotalSeconds;
    private static readonly TimeSpan AdminAccountLifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan UserAccountLifetime = TimeSpan.FromDays(30);
    private static readonly int SessionAccessExpirationTime = (int)TimeSpan.FromDays(30).TotalSeconds;

    // Keeps the cookie small; when exceeded the oldest unlocks are forgotten and the password is asked again
    private const int MaxUnlockedSessions = 20;

    private readonly IDataProtector _userProtector;
    private readonly ITimeLimitedDataProtector _accountProtector;
    private readonly IDataProtector _sessionAccessProtector;
    private readonly Dictionary<GameKind, (string CookieName, IDataProtector Protector)> _gamePlayerCookies;

    public SignedCookies(IDataProtectionProvider dataProtectionProvider)
    {
        _userProtector = dataProtectionProvider.CreateProtector("PartyKing.UserCookie");
        _accountProtector = dataProtectionProvider.CreateProtector("PartyKing.AccountCookie").ToTimeLimitedDataProtector();
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

    public void GrantAccount(AccountIdentity identity, HttpContext context)
    {
        var lifetime = identity.Role == AccountRole.Admin ? AdminAccountLifetime : UserAccountLifetime;

        // The lifetime is signed into the value, so a copied cookie can't be kept alive by editing its expiry
        context.Response.Cookies.Append(
            CookieNames.Account,
            _accountProtector.Protect($"{identity.Id:N}:{identity.SecurityStamp:N}:{identity.Role}", lifetime),
            GetOptions((int)lifetime.TotalSeconds, context));
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

    public bool TryReadAccount(HttpRequest request, out Guid accountId, out Guid securityStamp, out AccountRole role)
    {
        accountId = Guid.Empty;
        securityStamp = Guid.Empty;
        role = AccountRole.User;

        // An expired cookie fails to unprotect like a tampered one
        if (!request.Cookies.TryGetValue(CookieNames.Account, out var protectedValue)
            || !TryUnprotect(_accountProtector, protectedValue, out var value))
        {
            return false;
        }

        return value.Split(':') is [var id, var stamp, var roleName]
            && Guid.TryParseExact(id, "N", out accountId)
            && Guid.TryParseExact(stamp, "N", out securityStamp)
            && Enum.TryParse(roleName, out role)
            && Enum.IsDefined(role);
    }

    public void RevokeAccount(HttpContext context)
    {
        context.Response.Cookies.Delete(CookieNames.Account, new CookieOptions { Path = "/" });
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

    public void GrantGamePlayer(GameKind game, Guid sessionId, GameSeat seat, HttpContext context)
    {
        var (cookieName, protector) = _gamePlayerCookies[game];

        var players = ReadGamePlayers(game, context.Request)
            .Where(x => x.SessionId != sessionId)
            .Append((SessionId: sessionId, Seat: seat))
            .TakeLast(MaxUnlockedSessions);

        // "session:player" for a seat that was never taken over, "session:player:stamp" after it
        context.Response.Cookies.Append(
            cookieName,
            protector.Protect(string.Join(';', players.Select(x => x.Seat.Stamp is { } stamp
                ? $"{x.SessionId:N}:{x.Seat.PlayerId:N}:{stamp:N}"
                : $"{x.SessionId:N}:{x.Seat.PlayerId:N}"))),
            GetOptions(SessionAccessExpirationTime, context));
    }

    public bool TryReadGamePlayer(GameKind game, HttpRequest request, Guid sessionId, out GameSeat seat)
    {
        seat = ReadGamePlayers(game, request).LastOrDefault(x => x.SessionId == sessionId).Seat;

        return seat.PlayerId != Guid.Empty;
    }

    private IEnumerable<(Guid SessionId, GameSeat Seat)> ReadGamePlayers(GameKind game, HttpRequest request)
    {
        var (cookieName, protector) = _gamePlayerCookies[game];
        if (!request.Cookies.TryGetValue(cookieName, out var protectedValue)
            || !TryUnprotect(protector, protectedValue, out var value))
        {
            yield break;
        }

        foreach (var entry in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split(':');
            if (parts.Length is 2 or 3
                && Guid.TryParseExact(parts[0], "N", out var sessionId)
                && Guid.TryParseExact(parts[1], "N", out var playerId))
            {
                Guid? stamp = null;
                if (parts.Length == 3)
                {
                    // A stamp that doesn't parse is a damaged entry, not a seat that was never taken over
                    if (!Guid.TryParseExact(parts[2], "N", out var parsed))
                    {
                        continue;
                    }

                    stamp = parsed;
                }

                yield return (sessionId, new GameSeat(playerId, stamp));
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
