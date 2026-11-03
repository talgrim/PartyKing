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
    }
}
