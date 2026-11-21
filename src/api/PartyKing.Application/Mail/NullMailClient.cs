using Microsoft.Extensions.Logging;
using PartyKing.Application.Ports;

namespace PartyKing.Application.Mail;

/// <summary>
/// Used while no mail provider is configured (local development, the e2e environment, the demo): nothing is sent, the
/// subject is logged. The mail provider's registration replaces it.
/// </summary>
internal sealed class NullMailClient : IMailClient
{
    private readonly ILogger<NullMailClient> _logger;

    public NullMailClient(ILogger<NullMailClient> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(MailMessage message, CancellationToken cancellationToken)
    {
        _logger.LogWarning("No mail provider is configured, so this mail was not sent: {Subject}", message.Subject);

        return Task.CompletedTask;
    }
}
