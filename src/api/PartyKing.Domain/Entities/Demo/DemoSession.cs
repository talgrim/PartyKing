namespace PartyKing.Domain.Entities.Demo;

/// <summary>
/// Marks a session that the demo made for a visitor. It lives until <see cref="ExpiresAt"/> and is deleted then, with
/// the synthetic host the demo made for it.
/// </summary>
public class DemoSession
{
    private DemoSession()
    {
    }

    internal DemoSession(Guid sessionId, string hostProfileId, DateTime expiresAt)
    {
        SessionId = sessionId;
        HostProfileId = hostProfileId;
        ExpiresAt = expiresAt;
    }

    public Guid SessionId { get; internal set; }

    /// <summary>The synthetic Spotify profile the demo made to host the session, deleted with it.</summary>
    public string HostProfileId { get; internal set; } = null!;

    /// <summary>UTC.</summary>
    public DateTime ExpiresAt { get; internal set; }

    /// <summary>Makes the marker of a session that ends <paramref name="lifetime"/> after <paramref name="now"/> (UTC).</summary>
    public static DemoSession Start(Guid sessionId, string hostProfileId, DateTime now, TimeSpan lifetime)
    {
        return new DemoSession(sessionId, hostProfileId, now.Add(lifetime));
    }

    /// <summary>A demo session is over from its expiration instant on.</summary>
    public bool IsExpired(DateTime now)
    {
        return ExpiresAt <= now;
    }
}
