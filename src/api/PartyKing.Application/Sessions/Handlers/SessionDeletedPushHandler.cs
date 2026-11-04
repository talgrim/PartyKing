using PartyKing.Application.Events;
using PartyKing.Application.Realtime;
using PartyKing.Domain.Events.Sessions;

namespace PartyKing.Application.Sessions.Handlers;

/// <summary>
/// Tells the clients still connected to a deleted session that what they show is gone. The cascade delete took the queue and the
/// games with it, so the existing pushes are enough: the game pushes make every client fetch again and get its 404, and an empty
/// queue advance clears the song on display (the queue list follows with its next poll).
/// </summary>
internal sealed class SessionDeletedPushHandler : IDomainEventHandler<SessionDeleted>
{
    private readonly IRingoNotifier _ringo;
    private readonly IResistanceNotifier _resistance;
    private readonly IQueueNotifier _queue;

    public SessionDeletedPushHandler(IRingoNotifier ringo, IResistanceNotifier resistance, IQueueNotifier queue)
    {
        _ringo = ringo;
        _resistance = resistance;
        _queue = queue;
    }

    public async Task HandleAsync(SessionDeleted domainEvent, CancellationToken cancellationToken)
    {
        await _ringo.ChangedAsync(domainEvent.SessionId, cancellationToken);
        await _resistance.ChangedAsync(domainEvent.SessionId, cancellationToken);
        await _queue.QueueAdvancedAsync(domainEvent.SessionId, null, null, cancellationToken);
    }
}
