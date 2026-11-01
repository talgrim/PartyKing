using PartyKing.Domain.Entities.Games;

namespace PartyKing.Application.Tests;

/// <summary>The flip and continue rules of <see cref="RingoGame"/>, with no database.</summary>
public class RingoGameTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    // Card ids run by suit, 13 ranks each, 2 to ace: the king is the rank at index 11
    private static readonly int[] Kings = [11, 24, 37, 50];

    private readonly RingoGame _game = new() { SessionId = Guid.NewGuid() };

    private RingoPlayer AddPlayer(string name)
    {
        var player = new RingoPlayer
        {
            SessionId = _game.SessionId,
            PlayerId = Guid.NewGuid(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Order = _game.Players.Count,
        };
        _game.Players.Add(player);
        _game.CurrentPlayerId ??= player.PlayerId;

        return player;
    }

    [Fact]
    public void Flip_RecordsTheCardUnderTheGivenNameAndMakesItPending()
    {
        var outcome = _game.Flip(5, "guest", null, Now);

        Assert.Null(outcome.Failure);
        Assert.True(outcome.Changed);
        var flip = Assert.Single(_game.Flips);
        Assert.Equal((0, 5, "guest", Now), (flip.Order, flip.CardId, flip.FlippedBy, flip.FlippedAt));
        Assert.Equal(_game.SessionId, flip.SessionId);
        Assert.Equal(5, _game.PendingCardId);
    }

    [Fact]
    public void Flip_WithPlayers_IsRecordedAsTheCurrentPlayerAndKeepsTheirTurn()
    {
        var ola = AddPlayer("Ola");
        AddPlayer("Bartek");

        _game.Flip(5, "admin", null, Now);

        Assert.Equal("Ola", Assert.Single(_game.Flips).FlippedBy);
        Assert.Equal(ola.PlayerId, _game.CurrentPlayerId);
    }

    [Fact]
    public void Flip_ChecksTheTurnBeforeEverythingElse()
    {
        AddPlayer("Ola");
        var bartek = AddPlayer("Bartek");
        _game.Flip(5, "admin", null, Now);

        var outcome = _game.Flip(5, "admin", bartek.PlayerId, Now);

        Assert.Equal(RingoFailure.NotYourTurn, outcome.Failure);
        Assert.False(outcome.Changed);
    }

    [Fact]
    public void Flip_OfAFlippedCard_IsAcceptedButChangesNothing_EvenWhileACardIsPending()
    {
        _game.Flip(5, "admin", null, Now);

        var outcome = _game.Flip(5, "admin", null, Now);

        Assert.Null(outcome.Failure);
        Assert.False(outcome.Changed);
        Assert.Single(_game.Flips);
    }

    [Fact]
    public void Flip_WhileAnotherCardIsPending_IsRefused()
    {
        _game.Flip(5, "admin", null, Now);

        var outcome = _game.Flip(6, "admin", null, Now);

        Assert.Equal(RingoFailure.CardPending, outcome.Failure);
        Assert.False(outcome.Changed);
        Assert.Equal(5, _game.PendingCardId);
    }

    [Fact]
    public void Flip_AfterTheFourthKing_IsRefusedAsGameOver()
    {
        foreach (var king in Kings)
        {
            Assert.True(_game.Flip(king, "admin", null, Now).Changed);
            _game.Continue(null);
        }

        var outcome = _game.Flip(2, "admin", null, Now);

        Assert.True(_game.IsOver);
        Assert.Equal(RingoFailure.GameOver, outcome.Failure);
        Assert.Equal(4, _game.Flips.Count);
    }

    [Fact]
    public void Continue_ClearsThePendingCardAndPassesTheTurnRoundTheTable()
    {
        var ola = AddPlayer("Ola");
        var bartek = AddPlayer("Bartek");
        _game.Flip(5, "admin", ola.PlayerId, Now);

        var first = _game.Continue(ola.PlayerId);
        _game.Flip(6, "admin", bartek.PlayerId, Now);
        var second = _game.Continue(bartek.PlayerId);

        Assert.True(first.Changed);
        Assert.True(second.Changed);
        Assert.Null(_game.PendingCardId);
        Assert.Equal(ola.PlayerId, _game.CurrentPlayerId);
    }

    [Fact]
    public void Continue_ByAnotherPlayer_IsRefusedAndChangesNothing()
    {
        var ola = AddPlayer("Ola");
        var bartek = AddPlayer("Bartek");
        _game.Flip(5, "admin", ola.PlayerId, Now);

        var outcome = _game.Continue(bartek.PlayerId);

        Assert.Equal(RingoFailure.NotYourTurn, outcome.Failure);
        Assert.False(outcome.Changed);
        Assert.Equal(5, _game.PendingCardId);
    }

    [Fact]
    public void Continue_WithoutAPendingCard_IsAcceptedButChangesNothing()
    {
        var ola = AddPlayer("Ola");
        AddPlayer("Bartek");

        var outcome = _game.Continue(ola.PlayerId);

        Assert.Null(outcome.Failure);
        Assert.False(outcome.Changed);
        Assert.Equal(ola.PlayerId, _game.CurrentPlayerId);
    }
}
