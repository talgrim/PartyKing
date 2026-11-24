namespace PartyKing.Application.Ports;

/// <summary>A plain-text mail. The sender is the client's own configuration.</summary>
public sealed record MailMessage(string To, string Subject, string Body);

public interface IMailClient
{
    /// <summary>Sends the mail. Throws when the provider refuses it or cannot be reached.</summary>
    Task SendAsync(MailMessage message, CancellationToken cancellationToken);
}
