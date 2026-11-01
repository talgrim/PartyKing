using PartyKing.Domain.Events;

namespace PartyKing.Application.Events;

/// <summary>Drops the events; for tests of code that does not care about them.</summary>
internal sealed class NullDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
