using Microsoft.AspNetCore.Http;
using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Application.Ports;

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

    /// <summary>Remembers the player this browser joined the session's game as.</summary>
    void GrantGamePlayer(GameKind game, Guid sessionId, Guid playerId, HttpContext? httpContext);

    /// <summary>Reads the player id stored for the session's game; false when there is none or the cookie was tampered with.</summary>
    bool TryReadGamePlayer(GameKind game, HttpRequest request, Guid sessionId, out Guid playerId);
}
