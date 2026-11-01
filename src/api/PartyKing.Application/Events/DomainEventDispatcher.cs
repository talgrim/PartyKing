using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PartyKing.Domain.Events;

namespace PartyKing.Application.Events;

/// <summary>
/// In-memory dispatcher: no queue and no outbox, the handlers run before <see cref="DispatchAsync"/> returns. Each event
/// gets its own scope (repositories are singletons, handlers may be scoped), and one failing handler neither stops the
/// others nor reaches the caller, whose change is committed already.
/// </summary>
internal sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, Invoker> Invokers = new();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IServiceScopeFactory scopeFactory, ILogger<DomainEventDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in events)
        {
            await DispatchOneAsync(domainEvent, cancellationToken);
        }
    }

    private async Task DispatchOneAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var invoker = Invokers.GetOrAdd(domainEvent.GetType(), CreateInvoker);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await invoker.InvokeAsync(scope.ServiceProvider, domainEvent, _logger, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dispatching {Event} failed", domainEvent.GetType().Name);
        }
    }

    private static Invoker CreateInvoker(Type eventType)
    {
        return (Invoker)Activator.CreateInstance(typeof(Invoker<>).MakeGenericType(eventType))!;
    }

    private abstract class Invoker
    {
        public abstract Task InvokeAsync(
            IServiceProvider services, IDomainEvent domainEvent, ILogger logger, CancellationToken cancellationToken);
    }

    private sealed class Invoker<TEvent> : Invoker
        where TEvent : IDomainEvent
    {
        public override async Task InvokeAsync(
            IServiceProvider services, IDomainEvent domainEvent, ILogger logger, CancellationToken cancellationToken)
        {
            foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
            {
                try
                {
                    await handler.HandleAsync((TEvent)domainEvent, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "{Handler} failed on {Event}", handler.GetType().Name, typeof(TEvent).Name);
                }
            }
        }
    }
}
