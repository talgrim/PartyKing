namespace PartyKing.Application.Configuration;

/// <summary>
/// The mail the server sends to its operator (today: a guest asked for a session to be deleted). The defaults point at
/// Resend, whose key belongs in <c>MailSettings:ApiKey</c> (a secret, never in appsettings.json). Without a key and a
/// sender nothing is sent and the mail is only logged.
/// </summary>
public class MailConfiguration
{
    public const string SectionName = "MailSettings";

    public string BaseUrl { get; set; } = "https://api.resend.com/";

    public string? ApiKey { get; set; }

    /// <summary>The address the mail comes from; it must be on a domain the provider has verified.</summary>
    public string? Sender { get; set; }

    /// <summary>Where a reply to the mail goes, so it reaches the operator even when the sender has no mailbox. Empty sets none.</summary>
    public string? ReplyTo { get; set; }

    /// <summary>Who is told about a deletion request. Empty means nobody is, and the request is logged.</summary>
    public string? Recipient { get; set; }
}
