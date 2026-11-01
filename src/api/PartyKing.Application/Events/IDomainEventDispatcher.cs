using PartyKing.Domain.Events;

namespace PartyKing.Application.Events;

/// <summary>Hands the events an aggregate raised to their handlers. Called by the repository once the change is saved and its lock is released.</summary>
public interface IDomainEventDispatcher
{
    /// <summary>Runs every handler of every event, in event order. Never throws because of a handler.</summary>
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken);
}
