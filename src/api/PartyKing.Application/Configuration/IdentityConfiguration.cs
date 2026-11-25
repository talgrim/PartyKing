namespace PartyKing.Application.Configuration;

/// <summary>
/// What PartyKing decides about the identity service (auth.talgrim.dev). The rest of the <c>Identity</c> section (address,
/// cookie name, cache times, timeout) is the options of the client package, <c>IdentityClientOptions</c>. With
/// <see cref="Enabled"/> off (the demo) nobody is ever signed in and the service is never called.
/// </summary>
public class IdentityConfiguration
{
    public const string SectionName = "Identity";

    public bool Enabled { get; set; } = true;

    /// <summary>Shared secret that signs the events it sends (the account deletion webhook). A secret: never in appsettings.json.</summary>
    public string? WebhookSecret { get; set; }
}
