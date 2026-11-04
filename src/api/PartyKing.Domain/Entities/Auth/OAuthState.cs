using System.Security.Cryptography;

namespace PartyKing.Domain.Entities.Auth;

/// <summary>Short-lived record tying a Spotify OAuth round trip to the session being claimed.</summary>
public class OAuthState
{
    /// <summary>How long the host has to finish the Spotify login after the link was made.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private const int IdBytes = 24;

    private OAuthState()
    {
    }

    internal OAuthState(string id, Guid sessionId, DateTime expiresAt)
    {
        Id = id;
        SessionId = sessionId;
        ExpiresAt = expiresAt;
    }

    /// <summary>Random, unguessable value sent to Spotify as the OAuth state parameter.</summary>
    public string Id { get; internal set; } = null!;
    public Guid SessionId { get; internal set; }

    /// <summary>UTC.</summary>
    public DateTime ExpiresAt { get; internal set; }

    /// <summary>
    /// Makes a state for the session, valid for <see cref="Lifetime"/> from <paramref name="now"/> (UTC). The id is made here,
    /// unlike the ids of the games and sessions: it is a secret, and how unguessable it is belongs to the rule.
    /// </summary>
    public static OAuthState Issue(Guid sessionId, DateTime now)
    {
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(IdBytes));

        return new OAuthState(id, sessionId, now.Add(Lifetime));
    }

    /// <summary>A state is good through its expiration instant and refused after it.</summary>
    public bool IsExpired(DateTime now)
    {
        return ExpiresAt < now;
    }
}
