using PartyKing.Application.Demo.Models;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.API.Cookies;

public interface ISignedCookies
{
    void GrantHost(string profileId, HttpContext context);
    void GrantSpotifyAccessToken(string spotifyAccessToken, int expiresIn, HttpContext context);

    /// <summary>Reads the host's Spotify profile id; false when the cookie is missing or was tampered with.</summary>
    bool TryReadProfileId(HttpRequest request, out string profileId);

    /// <summary>Remembers that this browser entered the session's password; <paramref name="stamp"/> ties it to that password.</summary>
    void GrantSessionAccess(Guid sessionId, string stamp, HttpContext context);

    /// <summary>Reads the stamp stored for the session; false when there is none or the cookie was tampered with.</summary>
    bool TryReadSessionAccess(HttpRequest request, Guid sessionId, out string stamp);

    /// <summary>
    /// Remembers that this browser started the demo session as the host with the synthetic profile
    /// <paramref name="profileId"/>, so it can host it again after another start replaced the host cookie.
    /// </summary>
    void GrantDemoSession(Guid sessionId, string profileId, HttpContext context);

    /// <summary>The demo sessions this browser started, the oldest first; nothing is checked against the database.</summary>
    IReadOnlyList<StartedDemoSession> ReadDemoSessions(HttpRequest request);

    /// <summary>Remembers the seat (player and stamp) this browser joined the session's game as.</summary>
    void GrantGamePlayer(GameKind game, Guid sessionId, GameSeat seat, HttpContext context);

    /// <summary>Reads the seat stored for the session's game; false when there is none or the cookie was tampered with.</summary>
    bool TryReadGamePlayer(GameKind game, HttpRequest request, Guid sessionId, out GameSeat seat);
}
