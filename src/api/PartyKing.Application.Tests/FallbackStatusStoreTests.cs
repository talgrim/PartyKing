using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Queue.Services;

namespace PartyKing.Application.Tests;

public class FallbackStatusStoreTests
{
    private readonly FakePlayerNotifier _notifier = new();
    private readonly FallbackStatusStore _store;
    private readonly Guid _sessionId = Guid.NewGuid();

    public FallbackStatusStoreTests()
    {
        _store = new FallbackStatusStore(_notifier, NullLogger<FallbackStatusStore>.Instance);
    }

    [Fact]
    public async Task SetError_RemembersTheErrorAndPushesIt()
    {
        _store.SetError(_sessionId, "no tracks");

        Assert.Equal("no tracks", _store.GetError(_sessionId));
        await WaitForPushesAsync(1);
        Assert.Equal((_sessionId, "no tracks"), (_notifier.FallbackErrors[0].SessionId, _notifier.FallbackErrors[0].Error.Error));
    }

    [Fact]
    public async Task SetError_RepeatingTheSameError_PushesOnlyOnce()
    {
        _store.SetError(_sessionId, "no tracks");
        _store.SetError(_sessionId, "no tracks");
        _store.SetError(_sessionId, "other");

        await WaitForPushesAsync(2);
        Assert.Equal(["no tracks", "other"], _notifier.FallbackErrors.Select(x => x.Error.Error));
    }

    [Fact]
    public async Task Clear_AfterAnError_PushesNull()
    {
        _store.SetError(_sessionId, "no tracks");
        _store.Clear(_sessionId);

        await WaitForPushesAsync(2);
        Assert.Null(_store.GetError(_sessionId));
        Assert.Null(_notifier.FallbackErrors[1].Error.Error);
    }

    [Fact]
    public async Task Clear_WithoutAnError_PushesNothing()
    {
        _store.Clear(_sessionId);

        await Task.Delay(50);
        Assert.Empty(_notifier.FallbackErrors);
    }

    // The push is not awaited by the store, but the fake completes synchronously
    private async Task WaitForPushesAsync(int count)
    {
        for (var i = 0; i < 100 && _notifier.FallbackErrors.Count < count; i++)
        {
            await Task.Delay(10);
        }
    }
}
