using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.API.Cookies;

public interface ISignedCookies
{
    void GrantHost(string profileId, HttpContext context);
    void GrantSpotifyAccessToken(string spotifyAccessToken, int expiresIn, HttpContext context);
    void GrantAdmin(Guid adminId, HttpContext context);

    /// <summary>Reads the host's Spotify profile id; false when the cookie is missing or was tampered with.</summary>
    bool TryReadProfileId(HttpRequest request, out string profileId);

    /// <summary>Reads the admin account id; false when the cookie is missing or was tampered with.</summary>
    bool TryReadAdminId(HttpRequest request, out Guid adminId);
    void RevokeAdmin(HttpContext context);

    /// <summary>Remembers that this browser entered the session's password; <paramref name="stamp"/> ties it to that password.</summary>
    void GrantSessionAccess(Guid sessionId, string stamp, HttpContext context);

    /// <summary>Reads the stamp stored for the session; false when there is none or the cookie was tampered with.</summary>
    bool TryReadSessionAccess(HttpRequest request, Guid sessionId, out string stamp);

    /// <summary>Remembers the player this browser joined the session's game as.</summary>
    void GrantGamePlayer(GameKind game, Guid sessionId, Guid playerId, HttpContext context);

    /// <summary>Reads the player id stored for the session's game; false when there is none or the cookie was tampered with.</summary>
    bool TryReadGamePlayer(GameKind game, HttpRequest request, Guid sessionId, out Guid playerId);
}
