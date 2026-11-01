using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Events;
using PartyKing.Application.Games.Handlers;
using PartyKing.Application.Realtime;
using PartyKing.Domain.Entities.Base;
using PartyKing.Domain.Events;
using PartyKing.Domain.Events.Games.Base;
using PartyKing.Domain.Events.Games.Resistance;
using PartyKing.Domain.Events.Games.Ringo;

namespace PartyKing.Application.Tests;

/// <summary>Records what it is given and can run code per event, to look at the world at the moment of dispatch.</summary>
internal sealed class RecordingDispatcher : IDomainEventDispatcher
{
    public List<IDomainEvent> Events { get; } = [];

    public Func<IDomainEvent, Task>? OnDispatch { get; set; }

    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in events)
        {
            Events.Add(domainEvent);
            if (OnDispatch is not null)
            {
                await OnDispatch(domainEvent);
            }
        }
    }
}

internal static class TestEvents
{
    /// <summary>The real dispatcher with the game push handlers, over the given notifiers: what the API wires up.</summary>
    public static IDomainEventDispatcher Dispatcher(IRingoNotifier ringo, IResistanceNotifier resistance)
    {
        var services = new ServiceCollection();
        services.AddSingleton(ringo);
        services.AddSingleton(resistance);
        services.AddScoped<IDomainEventHandler<LobbyOpened>, LobbyPushHandler>();
        services.AddScoped<IDomainEventHandler<LobbyPlayerJoined>, LobbyPushHandler>();
        services.AddScoped<IDomainEventHandler<LobbyPlayerKicked>, LobbyPushHandler>();
        services.AddScoped<IDomainEventHandler<LobbyOrderChanged>, LobbyPushHandler>();
        services.AddScoped<IDomainEventHandler<LobbyGameEnded>, LobbyPushHandler>();
        services.AddScoped<IDomainEventHandler<RingoStarted>, RingoPushHandler>();
        services.AddScoped<IDomainEventHandler<RingoCardFlipped>, RingoPushHandler>();
        services.AddScoped<IDomainEventHandler<RingoTurnPassed>, RingoPushHandler>();
        services.AddScoped<IDomainEventHandler<ResistanceCreated>, ResistancePushHandler>();
        services.AddScoped<IDomainEventHandler<ResistanceChanged>, ResistancePushHandler>();

        return new DomainEventDispatcher(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DomainEventDispatcher>.Instance);
    }

    /// <summary>What a repository does after a save: hands the events the aggregate raised to the dispatcher.</summary>
    public static Task DispatchAsync(this IDomainEventDispatcher dispatcher, AggregateRoot aggregate)
    {
        return dispatcher.DispatchAsync(aggregate.DrainEvents(), CancellationToken.None);
    }
}
