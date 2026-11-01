using PartyKing.Domain.Events;

namespace PartyKing.Domain.Entities.Base;

/// <summary>
/// Base of the aggregates that raise <see cref="IDomainEvent"/>s. The events are collected while an action runs and taken
/// with <see cref="DrainEvents"/> once the change is saved. They are a field, not a property, so EF never maps them.
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _pendingEvents = [];

    protected void Raise(IDomainEvent domainEvent)
    {
        _pendingEvents.Add(domainEvent);
    }

    /// <summary>Returns the events raised since the last call, oldest first, and forgets them.</summary>
    public IReadOnlyList<IDomainEvent> DrainEvents()
    {
        if (_pendingEvents.Count == 0)
        {
            return [];
        }

        var events = _pendingEvents.ToArray();
        _pendingEvents.Clear();

        return events;
    }
}
