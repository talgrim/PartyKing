using PartyKing.Application.Player.Services;
using PartyKing.Contract.V1.Enums;

namespace PartyKing.Application.Tests;

public class PlayerStateStoreTests
{
    private readonly FakeClock _clock = new();
    private readonly PlayerStateStore _store;
    private readonly Guid _sessionId = Guid.NewGuid();

    public PlayerStateStoreTests()
    {
        _store = new PlayerStateStore(_clock);
    }

    [Fact]
    public void Get_BeforeAnyReport_IsNotConnected()
    {
        var snapshot = _store.Get(_sessionId);

        Assert.False(snapshot.Connected);
        Assert.Null(snapshot.SongId);
        Assert.False(snapshot.Paused);
    }

    [Fact]
    public void Report_MarksThePlayerConnectedAndRemembersWhatItPlays()
    {
        _store.Report(_sessionId, 5, paused: true);

        var snapshot = _store.Get(_sessionId);

        Assert.True(snapshot.Connected);
        Assert.Equal(5, snapshot.SongId);
        Assert.True(snapshot.Paused);
    }

    [Fact]
    public void Report_TheFirstTime_IsAChange()
    {
        Assert.True(_store.Report(_sessionId, 5, paused: false).Changed);
    }

    [Fact]
    public void Report_RepeatingTheSameState_IsNotAChange()
    {
        _store.Report(_sessionId, 5, paused: false);

        Assert.False(_store.Report(_sessionId, 5, paused: false).Changed);
    }

    [Fact]
    public void Report_AnotherSongOrPauseState_IsAChange()
    {
        _store.Report(_sessionId, 5, paused: false);

        Assert.True(_store.Report(_sessionId, 6, paused: false).Changed);
        Assert.True(_store.Report(_sessionId, 6, paused: true).Changed);
    }

    [Fact]
    public void Report_AfterTheTimeout_IsAChangeEvenForTheSameState()
    {
        _store.Report(_sessionId, 5, paused: false);
        _clock.Advance(PlayerStateStore.ConnectionTimeout + TimeSpan.FromSeconds(1));

        Assert.True(_store.Report(_sessionId, 5, paused: false).Changed);
    }

    [Fact]
    public void Get_AfterTheConnectionTimeout_IsNotConnected()
    {
        _store.Report(_sessionId, 5, paused: false);

        _clock.Advance(PlayerStateStore.ConnectionTimeout + TimeSpan.FromSeconds(1));

        Assert.False(_store.Get(_sessionId).Connected);
    }

    [Fact]
    public void Get_JustBeforeTheConnectionTimeout_IsStillConnected()
    {
        _store.Report(_sessionId, 5, paused: false);

        _clock.Advance(PlayerStateStore.ConnectionTimeout - TimeSpan.FromSeconds(1));

        Assert.True(_store.Get(_sessionId).Connected);
    }

    [Fact]
    public void Report_AfterTheTimeout_ConnectsAgain()
    {
        _store.Report(_sessionId, 5, paused: false);
        _clock.Advance(PlayerStateStore.ConnectionTimeout * 3);

        _store.Report(_sessionId, 5, paused: false);

        Assert.True(_store.Get(_sessionId).Connected);
    }

    [Fact]
    public void TrySendCommand_WithoutAPlayer_Fails()
    {
        Assert.False(_store.TrySendCommand(_sessionId, PlayerCommand.Pause));
    }

    [Fact]
    public void TrySendCommand_WithAGonePlayer_Fails()
    {
        _store.Report(_sessionId, 5, paused: false);
        _clock.Advance(PlayerStateStore.ConnectionTimeout * 2);

        Assert.False(_store.TrySendCommand(_sessionId, PlayerCommand.Pause));
    }

    [Fact]
    public void Report_HandsOverTheCommandOnce()
    {
        _store.Report(_sessionId, 5, paused: false);
        Assert.True(_store.TrySendCommand(_sessionId, PlayerCommand.Pause));

        Assert.Equal(PlayerCommand.Pause, _store.Report(_sessionId, 5, paused: false).Command);
        Assert.Null(_store.Report(_sessionId, 5, paused: true).Command);
    }

    [Fact]
    public void TrySendCommand_KeepsOnlyTheLatestCommand()
    {
        _store.Report(_sessionId, 5, paused: false);
        _store.TrySendCommand(_sessionId, PlayerCommand.Pause);
        _store.TrySendCommand(_sessionId, PlayerCommand.Resume);

        Assert.Equal(PlayerCommand.Resume, _store.Report(_sessionId, 5, paused: false).Command);
    }

    [Fact]
    public void Sessions_DoNotShareState()
    {
        var other = Guid.NewGuid();
        _store.Report(_sessionId, 5, paused: true);

        Assert.False(_store.Get(other).Connected);
        Assert.False(_store.TrySendCommand(other, PlayerCommand.Pause));
    }

    [Fact]
    public void Remove_ForgetsThePlayer()
    {
        _store.Report(_sessionId, 5, paused: false);

        _store.Remove(_sessionId);

        Assert.False(_store.Get(_sessionId).Connected);
        Assert.Null(_store.Get(_sessionId).SongId);
    }
}
