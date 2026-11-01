using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Events;
using PartyKing.Domain.Events;

namespace PartyKing.Application.Tests;

/// <summary>The in-memory dispatcher: which handlers run, in which scope, and what happens when one fails.</summary>
public class DomainEventDispatcherTests
{
    private readonly Calls _calls = new();

    private DomainEventDispatcher Create(Action<IServiceCollection> register, ILogger<DomainEventDispatcher>? logger = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_calls);
        register(services);

        return new DomainEventDispatcher(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            logger ?? NullLogger<DomainEventDispatcher>.Instance);
    }

    [Fact]
    public async Task EveryHandlerOfAnEvent_RunsWithThatEvent()
    {
        var dispatcher = Create(s =>
        {
            s.AddScoped<IDomainEventHandler<Ping>, FirstPingHandler>();
            s.AddScoped<IDomainEventHandler<Ping>, SecondPingHandler>();
        });

        await dispatcher.DispatchAsync([new Ping(7)], CancellationToken.None);

        Assert.Equal(["first:7", "second:7"], _calls.Log);
    }

    [Fact]
    public async Task Events_AreHandledInTheOrderTheyWereRaised_AndOnlyByTheirOwnHandlers()
    {
        var dispatcher = Create(s =>
        {
            s.AddScoped<IDomainEventHandler<Ping>, FirstPingHandler>();
            s.AddScoped<IDomainEventHandler<Pong>, PongHandler>();
        });

        await dispatcher.DispatchAsync([new Ping(1), new Pong(), new Ping(2)], CancellationToken.None);

        Assert.Equal(["first:1", "pong", "first:2"], _calls.Log);
    }

    [Fact]
    public async Task AnEventWithoutHandlers_IsIgnored()
    {
        var dispatcher = Create(_ => { });

        await dispatcher.DispatchAsync([new Ping(1)], CancellationToken.None);

        Assert.Empty(_calls.Log);
    }

    [Fact]
    public async Task AFailingHandler_DoesNotStopTheOthersOrReachTheCaller()
    {
        var dispatcher = Create(s =>
        {
            s.AddScoped<IDomainEventHandler<Ping>, ThrowingPingHandler>();
            s.AddScoped<IDomainEventHandler<Ping>, SecondPingHandler>();
            s.AddScoped<IDomainEventHandler<Pong>, PongHandler>();
        });

        await dispatcher.DispatchAsync([new Ping(1), new Pong()], CancellationToken.None);

        Assert.Equal(["throwing:1", "second:1", "pong"], _calls.Log);
    }

    [Fact]
    public async Task AFailingHandler_IsLogged()
    {
        var logger = new RecordingLogger();
        var dispatcher = Create(s => s.AddScoped<IDomainEventHandler<Ping>, ThrowingPingHandler>(), logger);

        await dispatcher.DispatchAsync([new Ping(1)], CancellationToken.None);

        var entry = Assert.Single(logger.Errors);
        Assert.Contains("ThrowingPingHandler", entry);
        Assert.Contains("Ping", entry);
    }

    [Fact]
    public async Task EachEvent_GetsItsOwnScope()
    {
        var dispatcher = Create(s =>
        {
            s.AddScoped<ScopeMarker>();
            s.AddScoped<IDomainEventHandler<Ping>, ScopedPingHandler>();
        });

        await dispatcher.DispatchAsync([new Ping(1), new Ping(2)], CancellationToken.None);

        Assert.Equal(2, _calls.Log.Distinct().Count());
    }

    internal sealed record Ping(int N) : IDomainEvent;

    internal sealed record Pong : IDomainEvent;

    internal sealed class Calls
    {
        public List<string> Log { get; } = [];
    }

    internal sealed class FirstPingHandler(Calls calls) : IDomainEventHandler<Ping>
    {
        public Task HandleAsync(Ping domainEvent, CancellationToken cancellationToken)
        {
            calls.Log.Add($"first:{domainEvent.N}");
            return Task.CompletedTask;
        }
    }

    internal sealed class SecondPingHandler(Calls calls) : IDomainEventHandler<Ping>
    {
        public Task HandleAsync(Ping domainEvent, CancellationToken cancellationToken)
        {
            calls.Log.Add($"second:{domainEvent.N}");
            return Task.CompletedTask;
        }
    }

    internal sealed class ThrowingPingHandler(Calls calls) : IDomainEventHandler<Ping>
    {
        public Task HandleAsync(Ping domainEvent, CancellationToken cancellationToken)
        {
            calls.Log.Add($"throwing:{domainEvent.N}");
            throw new InvalidOperationException("boom");
        }
    }

    internal sealed class PongHandler(Calls calls) : IDomainEventHandler<Pong>
    {
        public Task HandleAsync(Pong domainEvent, CancellationToken cancellationToken)
        {
            calls.Log.Add("pong");
            return Task.CompletedTask;
        }
    }

    internal sealed class ScopedPingHandler(Calls calls, ScopeMarker marker) : IDomainEventHandler<Ping>
    {
        public Task HandleAsync(Ping domainEvent, CancellationToken cancellationToken)
        {
            calls.Log.Add(marker.Id.ToString());
            return Task.CompletedTask;
        }
    }

    internal sealed class ScopeMarker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed class RecordingLogger : ILogger<DomainEventDispatcher>
    {
        public List<string> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
            }
        }
    }
}
