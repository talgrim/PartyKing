namespace PartyKing.Domain.Entities.Auth;

/// <summary>Short-lived record tying a Spotify OAuth round trip to the session being claimed.</summary>
public class OAuthState
{
    private OAuthState()
    {
    }

    public OAuthState(string id, Guid sessionId, DateTime expiresAt)
    {
        Id = id;
        SessionId = sessionId;
        ExpiresAt = expiresAt;
    }

    /// <summary>Random, unguessable value sent to Spotify as the OAuth state parameter.</summary>
    public string Id { get; set; } = null!;
    public Guid SessionId { get; set; }
    public DateTime ExpiresAt { get; set; }
}
