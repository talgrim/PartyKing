using PartyKing.Domain.Entities.Games.Base;
using PartyKing.Domain.Entities.Games.Resistance;
using PartyKing.Domain.Entities.Games.Ringo;
using PartyKing.Domain.Events.Games.Base;

namespace PartyKing.Application.Tests;

/// <summary>The events <see cref="LobbyGame{TPlayer}"/> raises for its lobby rules, with no database.</summary>
public class LobbyEventsTests
{
    private static RingoPlayer Player(string name) =>
        RingoPlayer.Create(Guid.NewGuid(), name, null, DateTimeOffset.UtcNow);

    private static RingoGame OpenGame() => new() { SessionId = Guid.NewGuid(), IsOpen = true };

    [Fact]
    public void Open_RaisesOpenedOnce_AndNothingForAGameThatIsOpenAlready()
    {
        var game = new RingoGame { SessionId = Guid.NewGuid() };

        game.Open();
        game.Open();

        Assert.Equal(new LobbyOpened(game.SessionId, GameKind.Ringo), Assert.Single(game.DrainEvents()));
    }

    [Fact]
    public void TheEvents_NameTheKindOfTheGame()
    {
        var game = new ResistanceGame { SessionId = Guid.NewGuid() };

        game.Open();

        Assert.Equal(new LobbyOpened(game.SessionId, GameKind.Resistance), Assert.Single(game.DrainEvents()));
    }

    [Fact]
    public void Join_RaisesJoinedForTheNewPlayer()
    {
        var game = OpenGame();
        var player = Player("Ola");

        game.Join(player);

        Assert.Equal(
            new LobbyPlayerJoined(game.SessionId, GameKind.Ringo, player.PlayerId), Assert.Single(game.DrainEvents()));
    }

    [Fact]
    public void ARefusedJoin_RaisesNothing()
    {
        var game = new RingoGame { SessionId = Guid.NewGuid() };
        var taken = OpenGame();
        taken.Join(Player("Ola"));
        taken.DrainEvents();

        Assert.Equal(LobbyStatus.NotOpen, game.Join(Player("Ola")));
        Assert.Equal(LobbyStatus.NameTaken, taken.Join(Player("OLA")));

        Assert.Empty(game.DrainEvents());
        Assert.Empty(taken.DrainEvents());
    }

    [Fact]
    public void Kick_RaisesKickedForThePlayer_AndNothingForAnUnknownOne()
    {
        var game = OpenGame();
        var player = Player("Ola");
        game.Join(player);
        game.DrainEvents();

        game.Kick(Guid.NewGuid());
        Assert.Empty(game.DrainEvents());

        game.Kick(player.PlayerId);
        Assert.Equal(
            new LobbyPlayerKicked(game.SessionId, GameKind.Ringo, player.PlayerId), Assert.Single(game.DrainEvents()));
    }

    [Fact]
    public void Reorder_RaisesOrderChanged_AndNothingWhenTheIdsDoNotMatch()
    {
        var game = OpenGame();
        var first = Player("Ola");
        var second = Player("Ala");
        game.Join(first);
        game.Join(second);
        game.DrainEvents();

        game.Reorder([first.PlayerId]);
        Assert.Empty(game.DrainEvents());

        game.Reorder([second.PlayerId, first.PlayerId]);
        Assert.Equal(new LobbyOrderChanged(game.SessionId, GameKind.Ringo), Assert.Single(game.DrainEvents()));
    }

    [Fact]
    public void DrainEvents_HandsOverTheEventsInOrderAndForgetsThem()
    {
        var game = new RingoGame { SessionId = Guid.NewGuid() };
        var player = Player("Ola");
        game.Open();
        game.Join(player);

        var events = game.DrainEvents();

        Assert.Equal(
            [typeof(LobbyOpened), typeof(LobbyPlayerJoined)], events.Select(x => x.GetType()));
        Assert.Empty(game.DrainEvents());
    }
}
