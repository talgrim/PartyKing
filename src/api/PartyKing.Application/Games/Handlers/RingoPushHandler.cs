using PartyKing.Application.Events;
using PartyKing.Application.Realtime;
using PartyKing.Domain.Events.Games.Ringo;

namespace PartyKing.Application.Games.Handlers;

/// <summary>
/// Tells the session's clients that the Ringo game changed: a card was flipped, or the pending card was dealt with and
/// the turn moved on. The game over event follows the flip it belongs to, so it needs no push of its own.
/// </summary>
internal sealed class RingoPushHandler :
    IDomainEventHandler<RingoCardFlipped>,
    IDomainEventHandler<RingoTurnPassed>
{
    private readonly IRingoNotifier _notifier;

    public RingoPushHandler(IRingoNotifier notifier)
    {
        _notifier = notifier;
    }

    public Task HandleAsync(RingoCardFlipped domainEvent, CancellationToken cancellationToken)
    {
        return _notifier.ChangedAsync(domainEvent.SessionId, cancellationToken);
    }

    public Task HandleAsync(RingoTurnPassed domainEvent, CancellationToken cancellationToken)
    {
        return _notifier.ChangedAsync(domainEvent.SessionId, cancellationToken);
    }
}
