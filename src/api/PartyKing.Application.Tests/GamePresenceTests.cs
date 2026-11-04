using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Realtime;
using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Application.Tests;

public class GamePresenceTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromMilliseconds(60);

    private readonly FakeResistanceNotifier _notifier = new();
    private readonly GamePresence _presence;
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _player = Guid.NewGuid();

    public GamePresenceTests()
    {
        _presence = new GamePresence(_notifier, NullLogger<GamePresence>.Instance, Grace);
    }

    private Task Connect(string connectionId, Guid? player = null, GameKind kind = GameKind.Resistance)
    {
        return _presence.ConnectAsync(kind, _sessionId, player ?? _player, connectionId);
    }

    private IReadOnlyList<Guid> Online(GameKind kind = GameKind.Resistance) => _presence.OnlinePlayers(kind, _sessionId);

    private async Task WaitForPushesAsync(int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            lock (_notifier.PresenceChanges)
            {
                if (_notifier.PresenceChanges.Count >= count)
                {
                    return;
                }
            }

            await Task.Delay(10);
        }

        throw new TimeoutException($"Fewer than {count} presence pushes arrived");
    }

    [Fact]
    public async Task Connect_MakesThePlayerOnlineAndPushesTheWholeList()
    {
        await Connect("c1");

        Assert.Equal([_player], Online());
        var push = Assert.Single(_notifier.PresenceChanges);
        Assert.Equal(_sessionId, push.SessionId);
        Assert.Equal([_player], push.Presence.OnlinePlayerIds);
    }

    [Fact]
    public async Task ASecondConnectionOfTheSamePlayerPushesNothingMore()
    {
        await Connect("c1");
        await Connect("c2");

        Assert.Single(_notifier.PresenceChanges);
    }

    [Fact]
    public async Task APlayerStaysOnlineWhileAnyOfTheirConnectionsLasts()
    {
        await Connect("c1");
        await Connect("c2");

        _presence.Disconnect("c1");
        await Task.Delay(Grace * 3);

        Assert.Equal([_player], Online());
        Assert.Single(_notifier.PresenceChanges);
    }

    [Fact]
    public async Task ThePlayerStaysOnlineDuringTheGraceAndGoesOfflineAfterIt()
    {
        await Connect("c1");

        _presence.Disconnect("c1");

        Assert.Equal([_player], Online());
        await WaitForPushesAsync(2);
        Assert.Empty(Online());
        Assert.Empty(_notifier.PresenceChanges[1].Presence.OnlinePlayerIds);
    }

    [Fact]
    public async Task AReconnectInsideTheGraceCancelsTheOfflinePush()
    {
        await Connect("c1");
        _presence.Disconnect("c1");

        await Connect("c2");
        await Task.Delay(Grace * 3);

        Assert.Equal([_player], Online());
        Assert.Single(_notifier.PresenceChanges);
    }

    [Fact]
    public async Task APlayerWhoLeftComesOnlineAgainWithANewPush()
    {
        await Connect("c1");
        _presence.Disconnect("c1");
        await WaitForPushesAsync(2);

        await Connect("c3");

        Assert.Equal(3, _notifier.PresenceChanges.Count);
        Assert.Equal([_player], _notifier.PresenceChanges[2].Presence.OnlinePlayerIds);
    }

    [Fact]
    public async Task APushListsEveryOnlinePlayerOfTheSessionOnly()
    {
        var other = Guid.NewGuid();
        await Connect("c1");
        await _presence.ConnectAsync(GameKind.Resistance, Guid.NewGuid(), Guid.NewGuid(), "elsewhere");
        await Connect("c2", other);

        Assert.Equal(new[] { _player, other }.Order(), _notifier.PresenceChanges[^1].Presence.OnlinePlayerIds.Order());
    }

    [Fact]
    public void DisconnectingAnUnknownConnectionChangesNothing()
    {
        _presence.Disconnect("never-connected");

        Assert.Empty(Online());
    }

    [Fact]
    public async Task OtherGamesAreTrackedButNothingIsPushed()
    {
        await Connect("c1", kind: GameKind.Ringo);

        Assert.Equal([_player], Online(GameKind.Ringo));
        Assert.Empty(Online());
        Assert.Empty(_notifier.PresenceChanges);
    }
}
