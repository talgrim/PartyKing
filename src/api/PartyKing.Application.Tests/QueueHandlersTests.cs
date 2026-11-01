using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Events;
using PartyKing.Application.Queue.Handlers;
using PartyKing.Application.Queue.Services;
using PartyKing.Application.Realtime;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Events;
using PartyKing.Domain.Events.Queue;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Tests;

/// <summary>What the queue events do: the pushes the services and the filler used to send, and the playlist refill.</summary>
public class QueueHandlersTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly FakeQueueNotifier _notifier = new();
    private readonly FakeFallbackQueueFiller _filler = new();

    private QueueItem Song(int id, bool isFallback = false) =>
        new(_sessionId, QueueItemSource.Spotify, $"spotify:track:{id}", $"Song {id}", "Artist", null, null)
        {
            Id = id,
            IsFallback = isFallback
        };

    private IDomainEventDispatcher Dispatcher()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IQueueNotifier>(_notifier);
        services.AddSingleton<IFallbackQueueFiller>(_filler);
        services.AddScoped<IDomainEventHandler<SongQueued>, QueuePushHandler>();
        services.AddScoped<IDomainEventHandler<SongRemoved>, QueuePushHandler>();
        services.AddScoped<IDomainEventHandler<QueueAdvanced>, QueuePushHandler>();
        services.AddScoped<IDomainEventHandler<SongRemoved>, FallbackRefillHandler>();
        services.AddScoped<IDomainEventHandler<QueueAdvanced>, FallbackRefillHandler>();

        return new DomainEventDispatcher(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DomainEventDispatcher>.Instance);
    }

    private Task DispatchAsync(params IDomainEvent[] events) => Dispatcher().DispatchAsync(events, Ct);

    [Fact]
    public async Task SongQueued_PushesTheSong_AndDoesNotRefill()
    {
        await DispatchAsync(new SongQueued(_sessionId, Song(3, isFallback: true)));

        var added = Assert.Single(_notifier.SongAdded);
        Assert.Equal(_sessionId, added.SessionId);
        Assert.Equal(3, added.Song.Id);
        Assert.True(added.Song.IsFallback);
        Assert.Empty(_filler.Ensured);
    }

    [Fact]
    public async Task SongRemoved_PushesTheRemoval_AndTopsUpTheQueue()
    {
        await DispatchAsync(new SongRemoved(_sessionId, 4));

        Assert.Equal((_sessionId, 4), Assert.Single(_notifier.SongRemoved));
        Assert.Equal(_sessionId, Assert.Single(_filler.Ensured));
    }

    [Fact]
    public async Task QueueAdvanced_PushesThePreviousAndTheNewSong_AndTopsUpTheQueue()
    {
        await DispatchAsync(new QueueAdvanced(_sessionId, 1, Song(2)));

        var advanced = Assert.Single(_notifier.QueueAdvanced);
        Assert.Equal(_sessionId, advanced.SessionId);
        Assert.Equal(1, advanced.PreviousSongId);
        Assert.Equal(2, advanced.NowPlaying?.Id);
        Assert.Equal(_sessionId, Assert.Single(_filler.Ensured));
    }

    [Fact]
    public async Task QueueAdvanced_WhenNothingIsLeft_PushesAnEmptyNowPlaying()
    {
        await DispatchAsync(new QueueAdvanced(_sessionId, 1, null));

        var advanced = Assert.Single(_notifier.QueueAdvanced);
        Assert.Equal(1, advanced.PreviousSongId);
        Assert.Null(advanced.NowPlaying);
    }

    [Fact]
    public async Task QueueAdvanced_PushesBeforeTheRefillStarts()
    {
        var pushedWhenRefilling = -1;
        _filler.OnEnsure = () => pushedWhenRefilling = _notifier.QueueAdvanced.Count;

        await DispatchAsync(new QueueAdvanced(_sessionId, 1, Song(2)));

        Assert.Equal(1, pushedWhenRefilling);
    }
}
