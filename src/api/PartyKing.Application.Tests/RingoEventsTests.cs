using PartyKing.Domain.Entities.Games.Ringo;
using PartyKing.Domain.Events;
using PartyKing.Domain.Events.Games.Base;
using PartyKing.Domain.Events.Games.Ringo;

namespace PartyKing.Application.Tests;

/// <summary>The events <see cref="RingoGame"/> raises, with no database.</summary>
public class RingoEventsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    // Card ids run by suit, 13 ranks each, 2 to ace: the king is the rank at index 11
    private static readonly int[] Kings = [11, 24, 37, 50];

    private readonly RingoGame _game = RingoGame.Start(Guid.NewGuid(), 7, Now);

    private static RingoPlayer Player(string name) =>
        RingoPlayer.Create(Guid.NewGuid(), name, null, Now);

    [Fact]
    public void Start_RaisesStarted()
    {
        Assert.Equal(new RingoStarted(_game.SessionId), Assert.Single(_game.DrainEvents()));
    }

    [Fact]
    public void Flip_RaisesTheCardWithTheRecordedName()
    {
        _game.DrainEvents();

        _game.Flip(5, "guest", null, Now);

        Assert.Equal(new RingoCardFlipped(_game.SessionId, 5, "guest"), Assert.Single(_game.DrainEvents()));
    }

    [Fact]
    public void Flip_WithPlayers_NamesThePlayerWhoseTurnItIs()
    {
        _game.IsOpen = true;
        _game.Join(Player("Ola"));
        _game.DrainEvents();

        _game.Flip(5, "admin", null, Now);

        Assert.Equal(new RingoCardFlipped(_game.SessionId, 5, "Ola"), Assert.Single(_game.DrainEvents()));
    }

    [Fact]
    public void AFlipThatChangesNothingOrFails_RaisesNothing()
    {
        _game.Flip(5, "guest", null, Now);
        _game.DrainEvents();

        var again = _game.Flip(5, "guest", null, Now);
        var pending = _game.Flip(6, "guest", null, Now);

        Assert.False(again.Changed);
        Assert.Equal(RingoFailure.CardPending, pending.Failure);
        Assert.Empty(_game.DrainEvents());
    }

    [Fact]
    public void Continue_RaisesTurnPassedToTheNextPlayer_AndNothingWithoutAPendingCard()
    {
        _game.IsOpen = true;
        var first = Player("Ola");
        var second = Player("Ala");
        _game.Join(first);
        _game.Join(second);
        _game.Flip(5, "guest", null, Now);
        _game.DrainEvents();

        _game.Continue(null);
        Assert.Equal(new RingoTurnPassed(_game.SessionId, second.PlayerId), Assert.Single(_game.DrainEvents()));

        _game.Continue(null);
        Assert.Empty(_game.DrainEvents());
    }

    [Fact]
    public void Continue_WithoutPlayers_PassesTheTurnToNobody()
    {
        _game.Flip(5, "guest", null, Now);
        _game.DrainEvents();

        _game.Continue(null);

        Assert.Equal(new RingoTurnPassed(_game.SessionId, null), Assert.Single(_game.DrainEvents()));
    }

    [Fact]
    public void TheFourthKing_RaisesGameOverRightAfterItsFlip()
    {
        foreach (var king in Kings.Take(3))
        {
            _game.Flip(king, "guest", null, Now);
            _game.Continue(null);
        }

        _game.DrainEvents();

        _game.Flip(Kings[3], "guest", null, Now);

        IDomainEvent[] expected =
        [
            new RingoCardFlipped(_game.SessionId, Kings[3], "guest"),
            new RingoGameOver(_game.SessionId, "guest"),
        ];
        Assert.Equal(expected, _game.DrainEvents());
    }

    [Fact]
    public void AKingBeforeTheFourth_DoesNotEndTheGame()
    {
        _game.DrainEvents();

        _game.Flip(Kings[0], "guest", null, Now);

        Assert.DoesNotContain(_game.DrainEvents(), x => x is RingoGameOver);
    }

    [Fact]
    public void KickingAPlayer_RaisesTheLobbyEventAndLeavesTheTurnToTheGame()
    {
        _game.IsOpen = true;
        var player = Player("Ola");
        _game.Join(player);
        _game.DrainEvents();

        _game.Kick(player.PlayerId);

        Assert.Equal(
            new LobbyPlayerKicked(_game.SessionId, PartyKing.Domain.Entities.Games.Base.GameKind.Ringo, player.PlayerId),
            Assert.Single(_game.DrainEvents()));
    }
}
