using Microsoft.Extensions.Logging;
using PartyKing.Application.Events;
using PartyKing.Domain.Events.Sessions;

namespace PartyKing.Application.Sessions.Handlers;

/// <summary>Writes the log line of every change to a session in one place.</summary>
internal sealed class SessionLogHandler :
    IDomainEventHandler<SessionRenamed>,
    IDomainEventHandler<SessionPasswordChanged>,
    IDomainEventHandler<HostClaimed>,
    IDomainEventHandler<HostReleased>,
    IDomainEventHandler<SessionDeleted>,
    IDomainEventHandler<SessionDeletionScheduled>,
    IDomainEventHandler<SessionDeletionCancelled>
{
    private readonly ILogger<SessionLogHandler> _logger;

    public SessionLogHandler(ILogger<SessionLogHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(SessionRenamed domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Renamed session {SessionId}", domainEvent.SessionId);

        return Task.CompletedTask;
    }

    public Task HandleAsync(SessionPasswordChanged domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Changed the password of session {SessionId}", domainEvent.SessionId);

        return Task.CompletedTask;
    }

    public Task HandleAsync(HostClaimed domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Profile {ProfileId} became the host of session {SessionId}", domainEvent.ProfileId, domainEvent.SessionId);

        return Task.CompletedTask;
    }

    public Task HandleAsync(HostReleased domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Released the host of session {SessionId}", domainEvent.SessionId);

        return Task.CompletedTask;
    }

    public Task HandleAsync(SessionDeleted domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleted session {SessionId}", domainEvent.SessionId);

        return Task.CompletedTask;
    }

    public Task HandleAsync(SessionDeletionScheduled domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Deletion of session {SessionId} was requested and is scheduled for {DeletionScheduledAt:u}",
            domainEvent.SessionId,
            domainEvent.DeletionScheduledAt);

        return Task.CompletedTask;
    }

    public Task HandleAsync(SessionDeletionCancelled domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cancelled the scheduled deletion of session {SessionId}", domainEvent.SessionId);

        return Task.CompletedTask;
    }
}
