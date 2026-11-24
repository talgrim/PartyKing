namespace PartyKing.Application.Configuration;

/// <summary>
/// The identity service that owns the accounts (auth.talgrim.dev). With <see cref="Enabled"/> off (the demo) nobody is
/// ever signed in and the service is never called.
/// </summary>
public class IdentityConfiguration
{
    public const string SectionName = "Identity";

    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "https://auth.talgrim.dev/";

    /// <summary>The session cookie the identity service sets (<c>Domain=.talgrim.dev</c>). PartyKing only forwards it.</summary>
    public string CookieName { get; set; } = "__Secure-talgrim.session";

    /// <summary>How long an answer is used without asking again, for a signed-in account and for an anonymous one.</summary>
    public int CacheSeconds { get; set; } = 30;

    public int NegativeCacheSeconds { get; set; } = 5;

    /// <summary>How long an answer for an account is kept for the case that the identity service is down.</summary>
    public int StaleSeconds { get; set; } = 300;

    public int TimeoutSeconds { get; set; } = 3;

    /// <summary>Shared secret that signs the events it sends (the account deletion webhook). A secret: never in appsettings.json.</summary>
    public string? WebhookSecret { get; set; }
}
