using PartyKing.Application.Events;
using PartyKing.Application.Realtime;
using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Application.Games.Handlers;

/// <summary>Tells the session's clients that the Resistance game changed: the payload-free push, each client then fetches its own view.</summary>
internal sealed class ResistancePushHandler : IDomainEventHandler<ResistanceChanged>
{
    private readonly IResistanceNotifier _notifier;

    public ResistancePushHandler(IResistanceNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task HandleAsync(ResistanceChanged domainEvent, CancellationToken cancellationToken)
    {
        return _notifier.ChangedAsync(domainEvent.SessionId, cancellationToken);
    }
}
