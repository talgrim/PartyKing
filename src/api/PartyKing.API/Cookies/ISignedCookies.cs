using PartyKing.Application.Accounts;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.API.Cookies;

public interface ISignedCookies
{
    void GrantHost(string profileId, HttpContext context);
    void GrantSpotifyAccessToken(string spotifyAccessToken, int expiresIn, HttpContext context);

    /// <summary>
    /// Signs the account in: stores its id and security stamp in the signed account cookie, which lives 12 hours for an
    /// admin and 30 days for a user (the lifetime is signed in, not only a cookie attribute).
    /// </summary>
    void GrantAccount(AccountIdentity identity, HttpContext context);

    /// <summary>Reads the host's Spotify profile id; false when the cookie is missing or was tampered with.</summary>
    bool TryReadProfileId(HttpRequest request, out string profileId);

    /// <summary>
    /// Reads the account id, the stamp and the role the account had when it logged in; false when the cookie is
    /// missing, expired or was tampered with. Nothing here was checked against the database: the role is only good
    /// for ruling an account out (a <see cref="AccountRole.User"/> cookie is never an admin's), not for trusting it.
    /// </summary>
    bool TryReadAccount(HttpRequest request, out Guid accountId, out Guid securityStamp, out AccountRole role);
    void RevokeAccount(HttpContext context);

    /// <summary>Remembers that this browser entered the session's password; <paramref name="stamp"/> ties it to that password.</summary>
    void GrantSessionAccess(Guid sessionId, string stamp, HttpContext context);

    /// <summary>Reads the stamp stored for the session; false when there is none or the cookie was tampered with.</summary>
    bool TryReadSessionAccess(HttpRequest request, Guid sessionId, out string stamp);

    /// <summary>Remembers the seat (player and stamp) this browser joined the session's game as.</summary>
    void GrantGamePlayer(GameKind game, Guid sessionId, GameSeat seat, HttpContext context);

    /// <summary>Reads the seat stored for the session's game; false when there is none or the cookie was tampered with.</summary>
    bool TryReadGamePlayer(GameKind game, HttpRequest request, Guid sessionId, out GameSeat seat);
}
