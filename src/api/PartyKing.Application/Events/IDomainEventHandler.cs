using PartyKing.Domain.Events;

namespace PartyKing.Application.Events;

/// <summary>
/// Reacts to a domain event after its change was committed: a push, a cleanup, a follow-up. Handlers run in their own
/// scope, so they may use scoped services, and may re-enter the repositories' locks. A handler that throws is logged
/// and never fails the request that raised the event.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
