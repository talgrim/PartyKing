using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Events;
using PartyKing.Application.Ports;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Domain.Events.Sessions;
using PartyKing.Domain.Repositories;

namespace PartyKing.Application.Sessions.Handlers;

/// <summary>
/// Tells the operator that a guest asked for a session to be deleted. The event is raised once per session, so one
/// request is one mail. A mail that fails is only logged by the dispatcher and the deletion stays scheduled; the log
/// line names the session, so the request is not lost.
/// </summary>
internal sealed class SessionDeletionMailHandler : IDomainEventHandler<SessionDeletionScheduled>
{
    private readonly ISessionsRepository _sessionsRepository;
    private readonly IMailClient _mailClient;
    private readonly MailConfiguration _configuration;
    private readonly ILogger<SessionDeletionMailHandler> _logger;

    public SessionDeletionMailHandler(
        ISessionsRepository sessionsRepository,
        IMailClient mailClient,
        IOptions<MailConfiguration> configuration,
        ILogger<SessionDeletionMailHandler> logger)
    {
        _sessionsRepository = sessionsRepository;
        _mailClient = mailClient;
        _configuration = configuration.Value;
        _logger = logger;
    }

    public async Task HandleAsync(SessionDeletionScheduled domainEvent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_configuration.Recipient))
        {
            _logger.LogWarning(
                "No mail recipient is configured, so nobody was told that deletion of session {SessionId} was requested for {DeletionScheduledAt:u}",
                domainEvent.SessionId,
                domainEvent.DeletionScheduledAt);

            return;
        }

        var session = await _sessionsRepository.GetAsync(domainEvent.SessionId, cancellationToken);

        if (session is null)
        {
            // Deleted between the request and this handler (the host was quicker): nothing left to tell
            _logger.LogInformation("Session {SessionId} is already gone, no deletion mail sent", domainEvent.SessionId);

            return;
        }

        var mail = SessionDeletionMail.Compose(_configuration.Recipient, session, domainEvent.DeletionScheduledAt);

        await _mailClient.SendAsync(mail, cancellationToken);

        _logger.LogInformation("Sent the deletion request mail of session {SessionId}", domainEvent.SessionId);
    }
}
