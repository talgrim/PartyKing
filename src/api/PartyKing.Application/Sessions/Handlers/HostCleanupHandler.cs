using PartyKing.Application.Events;
using PartyKing.Application.Sessions.Services;
using PartyKing.Domain.Events.Sessions;

namespace PartyKing.Application.Sessions.Handlers;

/// <summary>Forgets the session's player and the host's Spotify token when the host goes or the session is deleted.</summary>
internal sealed class HostCleanupHandler :
    IDomainEventHandler<HostReleased>,
    IDomainEventHandler<SessionDeleted>
{
    private readonly IHostSessionCleanup _cleanup;

    public HostCleanupHandler(IHostSessionCleanup cleanup)
    {
        _cleanup = cleanup;
    }

    public Task HandleAsync(HostReleased domainEvent, CancellationToken cancellationToken)
    {
        return _cleanup.ForgetHostAsync(domainEvent.SessionId, domainEvent.PreviousHostProfileId, cancellationToken);
    }

    public Task HandleAsync(SessionDeleted domainEvent, CancellationToken cancellationToken)
    {
        return _cleanup.ForgetHostAsync(domainEvent.SessionId, domainEvent.HostProfileId, cancellationToken);
    }
}
