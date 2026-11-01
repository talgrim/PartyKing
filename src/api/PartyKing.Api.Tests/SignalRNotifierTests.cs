using Microsoft.AspNetCore.SignalR;
using PartyKing.API.Hubs;
using PartyKing.API.Hubs.Notifiers;
using PartyKing.Application.Realtime;
using PartyKing.Contract.V1.Enums;
using PartyKing.Contract.V1.Lyrics;
using PartyKing.Contract.V1.Player;
using PartyKing.Contract.V1.Queue;

namespace PartyKing.Api.Tests;

public class SignalRNotifierTests
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly RecordingHubContext _hub = new();

    [Fact]
    public async Task QueueNotifier_PushesEachEventToTheSessionGroup()
    {
        IQueueNotifier notifier = new SignalRQueueNotifier(_hub);
        var song = new QueueItemDto(3, default, "url", "name", null, null, null);

        await notifier.SongAddedAsync(_sessionId, song, default);
        await notifier.SongRemovedAsync(_sessionId, 3, default);
        await notifier.QueueAdvancedAsync(_sessionId, 3, song, default);

        Assert.Equal(
            [SessionMusicHubEvents.SongAdded, SessionMusicHubEvents.SongRemoved, SessionMusicHubEvents.QueueAdvanced],
            _hub.Sent.Select(x => x.Method));
        Assert.All(_hub.Sent, x => Assert.Equal(SessionHub.GroupName(_sessionId), x.Group));
        Assert.Same(song, _hub.Sent[0].Args.Single());
        Assert.Equal(3, Assert.IsType<QueueItemRemovedDto>(_hub.Sent[1].Args.Single()).SongId);
        var advanced = Assert.IsType<QueueAdvancedDto>(_hub.Sent[2].Args.Single());
        Assert.Equal((3, song), (advanced.PreviousSongId, advanced.NowPlaying));
    }

    [Fact]
    public async Task PlayerNotifier_PushesStateCommandAndFallbackError()
    {
        IPlayerNotifier notifier = new SignalRPlayerNotifier(_hub);
        var state = new PlayerStateChangedDto(1, false, true);
        var command = new PlayerCommandDto(PlayerCommand.Pause);
        var error = new FallbackErrorDto("no tracks");

        await notifier.PlayerStateChangedAsync(_sessionId, state, default);
        await notifier.PlayerCommandAsync(_sessionId, command, default);
        await notifier.FallbackErrorChangedAsync(_sessionId, error, default);

        Assert.Equal(
            [
                (SessionHub.GroupName(_sessionId), SessionMusicHubEvents.PlayerStateChanged, (object)state),
                (SessionHub.GroupName(_sessionId), SessionMusicHubEvents.PlayerCommand, command),
                (SessionHub.GroupName(_sessionId), SessionMusicHubEvents.FallbackErrorChanged, error)
            ],
            _hub.Sent.Select(x => (x.Group, x.Method, x.Args.Single())));
    }

    [Fact]
    public async Task LyricsNotifier_PushesTheOffset()
    {
        var offset = new LyricsOffsetChangedDto(3, 250);

        await new SignalRLyricsNotifier(_hub).LyricsOffsetChangedAsync(_sessionId, offset, default);

        var sent = Assert.Single(_hub.Sent);
        Assert.Equal((SessionHub.GroupName(_sessionId), SessionMusicHubEvents.LyricsOffsetChanged), (sent.Group, sent.Method));
        Assert.Same(offset, sent.Args.Single());
    }

    [Fact]
    public async Task SlideshowNotifier_PushesSlideChangedWithoutPayload()
    {
        await new SignalRSlideshowNotifier(_hub).SlideshowChangedAsync(_sessionId, default);

        var sent = Assert.Single(_hub.Sent);
        Assert.Equal((SessionHub.GroupName(_sessionId), SessionMusicHubEvents.SlideChanged), (sent.Group, sent.Method));
        Assert.Empty(sent.Args);
    }

    [Fact]
    public async Task RingoNotifier_PushesOpenedAndChangedWithoutPayload()
    {
        IRingoNotifier notifier = new SignalRRingoNotifier(_hub);

        await notifier.OpenedAsync(_sessionId, default);
        await notifier.ChangedAsync(_sessionId, default);

        Assert.Equal([SessionMusicHubEvents.RingoOpened, SessionMusicHubEvents.RingoChanged], _hub.Sent.Select(x => x.Method));
        Assert.All(_hub.Sent, x => Assert.Equal(SessionHub.GroupName(_sessionId), x.Group));
        Assert.All(_hub.Sent, x => Assert.Empty(x.Args));
    }

    [Fact]
    public async Task ResistanceNotifier_PushesOpenedAndChangedWithoutPayload()
    {
        IResistanceNotifier notifier = new SignalRResistanceNotifier(_hub);

        await notifier.OpenedAsync(_sessionId, default);
        await notifier.ChangedAsync(_sessionId, default);

        Assert.Equal(
            [SessionMusicHubEvents.ResistanceOpened, SessionMusicHubEvents.ResistanceChanged],
            _hub.Sent.Select(x => x.Method));
        Assert.All(_hub.Sent, x => Assert.Equal(SessionHub.GroupName(_sessionId), x.Group));
        Assert.All(_hub.Sent, x => Assert.Empty(x.Args));
    }

    [Fact]
    public void EventNames_AreTheOnesTheUiListensFor()
    {
        Assert.Equal(
            [
                "SongAdded", "SongRemoved", "QueueAdvanced", "PlayerStateChanged", "PlayerCommand",
                "FallbackErrorChanged", "LyricsOffsetChanged", "SlideChanged", "RingoChanged", "RingoOpened",
                "ResistanceChanged", "ResistanceOpened"
            ],
            typeof(SessionMusicHubEvents).GetFields().Select(x => (string)x.GetRawConstantValue()!));
    }

    private sealed class RecordingHubContext : IHubContext<SessionHub>
    {
        public List<(string Group, string Method, object?[] Args)> Sent { get; } = [];

        public IHubClients Clients => new RecordingClients(this);
        public IGroupManager Groups => throw new NotSupportedException();

        private sealed class RecordingClients(RecordingHubContext owner) : IHubClients
        {
            public IClientProxy Group(string groupName) => new RecordingProxy(owner, groupName);

            public IClientProxy All => throw new NotSupportedException();
            public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
            public IClientProxy Client(string connectionId) => throw new NotSupportedException();
            public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
            public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
            public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
            public IClientProxy User(string userId) => throw new NotSupportedException();
            public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
        }

        private sealed class RecordingProxy(RecordingHubContext owner, string group) : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            {
                owner.Sent.Add((group, method, args));
                return Task.CompletedTask;
            }
        }
    }
}
