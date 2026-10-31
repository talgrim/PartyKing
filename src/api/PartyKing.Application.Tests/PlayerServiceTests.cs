using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Queue.Services;
using PartyKing.Application.Player.Services;
using PartyKing.Application.Realtime;
using PartyKing.Contract.V1.Enums;
using PartyKing.Contract.V1.Player;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Tests;

public class PlayerServiceTests
{
    private const string HostId = "host-profile";

    private readonly FakeClock _clock = new();
    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeQueueRepository _queue = new();
    private readonly FakeSessionMusicNotifier _notifier = new();
    private readonly PlayerStateStore _store;
    private readonly PlayerService _service;
    private readonly Session _session;

    public PlayerServiceTests()
    {
        _store = new PlayerStateStore(_clock);
        _service = new PlayerService(_sessions, _queue, _store, _notifier, new NullPlayerConnectionWatchdog(), new FakeFallbackQueueFiller(), new FallbackStatusStore(_notifier, NullLogger<FallbackStatusStore>.Instance), NullLogger<PlayerService>.Instance);
        _session = _sessions.AddSession(HostId);
    }

    private static QueueItem Item(int id, QueueItemStatus status = QueueItemStatus.Playing) =>
        new(Guid.NewGuid(), QueueItemSource.Spotify, $"spotify:track:{id}", $"Song {id}", "Artist", "https://art", 1000)
        {
            Id = id,
            Status = status
        };

    [Fact]
    public async Task GetNowPlaying_UnknownSession_IsNotFound()
    {
        var result = await _service.GetNowPlayingAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task GetNowPlaying_ReturnsTheSongWithItsArtAndArtist()
    {
        _queue.Playing = Item(3);
        _store.Report(_session.Id, 3, paused: false);

        var nowPlaying = (await _service.GetNowPlayingAsync(_session.Id, CancellationToken.None)).Value;

        Assert.Equal(3, nowPlaying.Song?.Id);
        Assert.Equal("Song 3", nowPlaying.Song?.Name);
        Assert.Equal("Artist", nowPlaying.Song?.Artists);
        Assert.Equal("https://art", nowPlaying.Song?.AlbumArtUrl);
        Assert.False(nowPlaying.Paused);
        Assert.True(nowPlaying.PlayerConnected);
    }

    [Fact]
    public async Task GetNowPlaying_NothingPlaying_HasNoSong()
    {
        var nowPlaying = (await _service.GetNowPlayingAsync(_session.Id, CancellationToken.None)).Value;

        Assert.Null(nowPlaying.Song);
        Assert.False(nowPlaying.PlayerConnected);
    }

    [Fact]
    public async Task GetNowPlaying_ReportsAPauseOfTheCurrentSong()
    {
        _queue.Playing = Item(3);
        _store.Report(_session.Id, 3, paused: true);

        var nowPlaying = (await _service.GetNowPlayingAsync(_session.Id, CancellationToken.None)).Value;

        Assert.True(nowPlaying.Paused);
    }

    [Fact]
    public async Task GetNowPlaying_ThePauseOfAnEarlierSongDoesNotCarryOver()
    {
        _store.Report(_session.Id, 3, paused: true);
        _queue.Playing = Item(4);

        var nowPlaying = (await _service.GetNowPlayingAsync(_session.Id, CancellationToken.None)).Value;

        Assert.False(nowPlaying.Paused);
    }

    [Fact]
    public async Task GetNowPlaying_CountsThePositionOnWhileTheSongPlays()
    {
        _queue.Playing = Item(3);
        _store.Report(_session.Id, 3, paused: false, positionMs: 20_000);
        _clock.Advance(TimeSpan.FromSeconds(5));

        var nowPlaying = (await _service.GetNowPlayingAsync(_session.Id, CancellationToken.None)).Value;

        Assert.Equal(25_000, nowPlaying.PositionMs);
    }

    [Fact]
    public async Task GetNowPlaying_ThePositionOfAnEarlierSongDoesNotCarryOver()
    {
        _store.Report(_session.Id, 3, paused: false, positionMs: 20_000);
        _queue.Playing = Item(4);

        var nowPlaying = (await _service.GetNowPlayingAsync(_session.Id, CancellationToken.None)).Value;

        Assert.Equal(0, nowPlaying.PositionMs);
    }

    [Fact]
    public async Task GetNowPlaying_AfterThePlayerWentAway_IsNotConnected()
    {
        _queue.Playing = Item(3);
        _store.Report(_session.Id, 3, paused: false);
        _clock.Advance(PlayerStateStore.ConnectionTimeout * 2);

        var nowPlaying = (await _service.GetNowPlayingAsync(_session.Id, CancellationToken.None)).Value;

        Assert.False(nowPlaying.PlayerConnected);
    }

    [Fact]
    public async Task ReportState_ByAnotherAccount_IsForbidden()
    {
        var result = await _service.ReportStateAsync(
            _session.Id, "someone-else", new PlayerStateRequestDto(1, false), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Forbidden, result.FirstError.Type);
        Assert.False(_store.Get(_session.Id).Connected);
    }

    [Fact]
    public async Task ReportState_UnknownSession_IsNotFound()
    {
        var result = await _service.ReportStateAsync(
            Guid.NewGuid(), HostId, new PlayerStateRequestDto(1, false), CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task ReportState_ByTheHost_ConnectsThePlayerAndReturnsTheAdminsCommand()
    {
        await _service.ReportStateAsync(_session.Id, HostId, new PlayerStateRequestDto(1, false), CancellationToken.None);
        await _service.SendCommandAsync(_session.Id, PlayerCommand.Pause, CancellationToken.None);

        var result = await _service.ReportStateAsync(
            _session.Id, HostId, new PlayerStateRequestDto(1, false), CancellationToken.None);

        Assert.Equal(PlayerCommand.Pause, result.Value.Command);
        Assert.Contains(
            _notifier.PlayerStateChanged,
            x => x.SessionId == _session.Id && x.State is { SongId: 1, Paused: false, PlayerConnected: true });
    }

    [Fact]
    public async Task ReportState_BroadcastsThePositionOfAChange()
    {
        await _service.ReportStateAsync(
            _session.Id, HostId, new PlayerStateRequestDto(1, true, PositionMs: 33_000), CancellationToken.None);

        Assert.Contains(_notifier.PlayerStateChanged, x => x.State is { SongId: 1, Paused: true, PositionMs: 33_000 });
    }

    [Fact]
    public async Task ReportState_ASeek_IsBroadcastEvenThoughNothingElseChanged()
    {
        await _service.ReportStateAsync(
            _session.Id, HostId, new PlayerStateRequestDto(1, false, PositionMs: 10_000), CancellationToken.None);
        await _service.ReportStateAsync(
            _session.Id, HostId, new PlayerStateRequestDto(1, false, PositionMs: 80_000, Seeked: true), CancellationToken.None);

        Assert.Equal(2, _notifier.PlayerStateChanged.Count);
        Assert.Equal(80_000, _notifier.PlayerStateChanged[1].State.PositionMs);
    }

    [Fact]
    public async Task ReportState_AKeepaliveWithAMovedOnPosition_DoesNotBroadcast()
    {
        await _service.ReportStateAsync(
            _session.Id, HostId, new PlayerStateRequestDto(1, false, PositionMs: 10_000), CancellationToken.None);
        await _service.ReportStateAsync(
            _session.Id, HostId, new PlayerStateRequestDto(1, false, PositionMs: 20_000), CancellationToken.None);

        Assert.Single(_notifier.PlayerStateChanged);
    }

    [Fact]
    public async Task ReportState_RepeatingTheSameState_DoesNotBroadcastAgain()
    {
        var request = new PlayerStateRequestDto(1, false);
        await _service.ReportStateAsync(_session.Id, HostId, request, CancellationToken.None);
        await _service.ReportStateAsync(_session.Id, HostId, request, CancellationToken.None);

        Assert.Single(_notifier.PlayerStateChanged);

        await _service.ReportStateAsync(_session.Id, HostId, new PlayerStateRequestDto(1, true), CancellationToken.None);

        Assert.Equal(2, _notifier.PlayerStateChanged.Count);
    }

    [Fact]
    public async Task SendCommand_ToAConnectedPlayer_PushesTheCommand()
    {
        await _service.ReportStateAsync(_session.Id, HostId, new PlayerStateRequestDto(1, false), CancellationToken.None);

        var result = await _service.SendCommandAsync(_session.Id, PlayerCommand.Pause, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Single(_notifier.PlayerCommands, x => x.SessionId == _session.Id && x.Command.Command == PlayerCommand.Pause);
    }

    [Fact]
    public async Task SendCommand_WithoutAConnectedPlayer_PushesNothing()
    {
        await _service.SendCommandAsync(_session.Id, PlayerCommand.Pause, CancellationToken.None);

        Assert.Empty(_notifier.PlayerCommands);
    }

    [Fact]
    public async Task SendCommand_WithoutAConnectedPlayer_IsAConflict()
    {
        var result = await _service.SendCommandAsync(_session.Id, PlayerCommand.Pause, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Conflict, result.FirstError.Type);
    }

    [Fact]
    public async Task Skip_WithoutAConnectedPlayer_IsAConflictAndAdvancesNothing()
    {
        var result = await _service.SkipAsync(_session.Id, 1, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.Conflict, result.FirstError.Type);
        Assert.Empty(_queue.AdvanceCalls);
    }

    [Fact]
    public async Task Skip_AdvancesFromTheSongTheAdminSaw()
    {
        _store.Report(_session.Id, 1, paused: false);
        _queue.Playing = Item(1);
        _queue.AdvanceResult = Item(2);

        var result = await _service.SkipAsync(_session.Id, 1, CancellationToken.None);

        Assert.Equal(2, result.Value.Id);
        Assert.Equal((_session.Id, (int?)1), Assert.Single(_queue.AdvanceCalls));
        var advanced = Assert.Single(_notifier.QueueAdvanced);
        Assert.Equal(1, advanced.PreviousSongId);
        Assert.Equal(2, advanced.NowPlaying?.Id);
    }

    [Fact]
    public async Task Skip_PastTheLastSong_IsNotFound()
    {
        _store.Report(_session.Id, 1, paused: false);
        _queue.Playing = Item(1);
        _queue.AdvanceResult = null;

        var result = await _service.SkipAsync(_session.Id, 1, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
        var advanced = Assert.Single(_notifier.QueueAdvanced);
        Assert.Equal(1, advanced.PreviousSongId);
        Assert.Null(advanced.NowPlaying);
    }
}
