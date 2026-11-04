using PartyKing.Application.Games.Handlers;
using PartyKing.Domain.Entities.Games.Base;
using PartyKing.Domain.Events.Games.Base;
using PartyKing.Domain.Events.Games.Resistance;
using PartyKing.Domain.Events.Games.Ringo;

namespace PartyKing.Application.Tests;

/// <summary>The handlers that turn game events into the payload-free pushes the clients react to.</summary>
public class GamePushHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly Guid _id = Guid.NewGuid();
    private readonly FakeRingoNotifier _ringo = new();
    private readonly FakeResistanceNotifier _resistance = new();
    private readonly LobbyPushHandler _lobby;

    public GamePushHandlerTests()
    {
        _lobby = new LobbyPushHandler(_ringo, _resistance);
    }

    [Theory]
    [InlineData(GameKind.Ringo)]
    [InlineData(GameKind.Resistance)]
    public async Task EveryLobbyEvent_PushesChangedToTheNotifierOfItsGameOnly(GameKind kind)
    {
        await _lobby.HandleAsync(new LobbyPlayerJoined(_id, kind, Guid.NewGuid()), Ct);
        await _lobby.HandleAsync(new LobbyPlayerKicked(_id, kind, Guid.NewGuid()), Ct);
        await _lobby.HandleAsync(new LobbyOrderChanged(_id, kind), Ct);
        await _lobby.HandleAsync(new LobbyGameEnded(_id, kind), Ct);

        var (pushed, untouched) = kind == GameKind.Ringo
            ? ((FakeGameLobbyNotifier)_ringo, (FakeGameLobbyNotifier)_resistance)
            : (_resistance, _ringo);
        Assert.Equal([_id, _id, _id, _id], pushed.Changes);
        Assert.Empty(pushed.Opens);
        Assert.Empty(untouched.Changes);
        Assert.Empty(untouched.Opens);
    }

    [Fact]
    public async Task OpeningALobby_PushesOpenedAndThenChanged()
    {
        await _lobby.HandleAsync(new LobbyOpened(_id, GameKind.Resistance), Ct);

        Assert.Equal([_id], _resistance.Opens);
        Assert.Equal([_id], _resistance.Changes);
        Assert.Empty(_ringo.Opens);
    }

    [Fact]
    public async Task RingoEvents_PushChanged_ButTheGameOverNeedsNoPushOfItsOwn()
    {
        var handler = new RingoPushHandler(_ringo);

        await handler.HandleAsync(new RingoStarted(_id), Ct);
        await handler.HandleAsync(new RingoCardFlipped(_id, 5, "guest"), Ct);
        await handler.HandleAsync(new RingoTurnPassed(_id, null), Ct);

        Assert.Equal([_id, _id, _id], _ringo.Changes);
        Assert.Empty(_resistance.Changes);
    }

    [Fact]
    public async Task ResistanceEvents_PushChanged()
    {
        var handler = new ResistancePushHandler(_resistance);

        await handler.HandleAsync(new ResistanceCreated(_id), Ct);
        await handler.HandleAsync(new ResistanceChanged(_id), Ct);

        Assert.Equal([_id, _id], _resistance.Changes);
        Assert.Empty(_ringo.Changes);
        Assert.Empty(_resistance.LobbyChanges);
    }

    [Fact]
    public async Task TheResistanceLobbyPush_FollowsTheEventsThatChangeWhatTheLobbyShows()
    {
        var handler = new ResistanceLobbyPushHandler(_resistance);

        await handler.HandleAsync(new ResistanceCreated(_id), Ct);
        await handler.HandleAsync(new ResistanceStarted(_id), Ct);
        await handler.HandleAsync(new ResistanceRestarted(_id), Ct);
        await handler.HandleAsync(new LobbyOpened(_id, GameKind.Resistance), Ct);
        await handler.HandleAsync(new LobbyPlayerJoined(_id, GameKind.Resistance, Guid.NewGuid()), Ct);
        await handler.HandleAsync(new LobbyPlayerKicked(_id, GameKind.Resistance, Guid.NewGuid()), Ct);
        await handler.HandleAsync(new LobbyGameEnded(_id, GameKind.Resistance), Ct);

        Assert.Equal(Enumerable.Repeat(_id, 7), _resistance.LobbyChanges);
        Assert.Empty(_resistance.Changes);
        Assert.Empty(_resistance.Opens);
        Assert.Empty(_ringo.Changes);
    }

    [Fact]
    public async Task TheResistanceLobbyPush_AlsoFollowsASeatBeingReleasedOrTakenOver()
    {
        var handler = new ResistanceLobbyPushHandler(_resistance);

        await handler.HandleAsync(new ResistanceSeatReleaseChanged(_id, Guid.NewGuid(), true), Ct);
        await handler.HandleAsync(new ResistanceSeatClaimed(_id, Guid.NewGuid()), Ct);

        Assert.Equal([_id, _id], _resistance.LobbyChanges);
        Assert.Empty(_resistance.Changes);
    }

    [Fact]
    public async Task TheResistanceLobbyPush_IgnoresTheLobbyEventsOfRingoAndAReorder()
    {
        var handler = new ResistanceLobbyPushHandler(_resistance);

        await handler.HandleAsync(new LobbyOpened(_id, GameKind.Ringo), Ct);
        await handler.HandleAsync(new LobbyPlayerJoined(_id, GameKind.Ringo, Guid.NewGuid()), Ct);
        await handler.HandleAsync(new LobbyPlayerKicked(_id, GameKind.Ringo, Guid.NewGuid()), Ct);
        await handler.HandleAsync(new LobbyGameEnded(_id, GameKind.Ringo), Ct);

        Assert.Empty(_resistance.LobbyChanges);
    }

    [Fact]
    public async Task TheActionsOfARound_PushTheGameButNotTheLobby()
    {
        var dispatcher = TestEvents.Dispatcher(_ringo, _resistance);

        await dispatcher.DispatchAsync([new ResistanceChanged(_id), new ResistanceVoteCast(_id, Guid.NewGuid())], Ct);
        await dispatcher.DispatchAsync([new LobbyOrderChanged(_id, GameKind.Resistance)], Ct);

        Assert.Equal([_id, _id], _resistance.Changes);
        Assert.Empty(_resistance.LobbyChanges);
    }

    [Fact]
    public async Task ARealDispatch_OfALifecycleEvent_PushesTheGameAndTheLobby()
    {
        var dispatcher = TestEvents.Dispatcher(_ringo, _resistance);

        await dispatcher.DispatchAsync([new ResistanceCreated(_id), new LobbyOpened(_id, GameKind.Resistance)], Ct);

        Assert.Equal([_id, _id], _resistance.LobbyChanges);
        Assert.Equal([_id, _id], _resistance.Changes);
        Assert.Equal([_id], _resistance.Opens);
    }
}
