using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Domain.Tests;

/// <summary>The switch deck of the Defector module (<see cref="ResistanceDefectorDeck"/>) on its own.</summary>
public class ResistanceDefectorDeckTests
{
    [Fact]
    public void ANewDeck_IsEmpty_AndFlipsNothing()
    {
        var deck = new ResistanceDefectorDeck();

        Assert.Empty(deck.Cards);
        Assert.Empty(deck.Flips(5));
        Assert.False(deck.ChangesAllegianceAt(3));
        Assert.False(deck.Switched(5));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    public void ADealtDeck_HasThreeNoChangeAndTwoChangeAllegiance(int seed)
    {
        var deck = new ResistanceDefectorDeck();

        deck.Deal(new Random(seed));

        Assert.Equal(5, deck.Cards.Length);
        Assert.Equal(3, deck.Cards.Count(x => x == 'N'));
        Assert.Equal(2, deck.Cards.Count(x => x == 'C'));
    }

    [Fact]
    public void Dealing_ShufflesTheDeck()
    {
        var decks = Enumerable.Range(0, 40).Select(seed =>
        {
            var deck = new ResistanceDefectorDeck();
            deck.Deal(new Random(seed));

            return deck.Cards;
        }).Distinct();

        Assert.True(decks.Count() > 1);
    }

    [Theory]
    [InlineData(1, "")]
    [InlineData(2, "")]
    [InlineData(3, "C")]
    [InlineData(4, "CN")]
    [InlineData(5, "CNN")]
    public void TheFlips_FollowTheMissionNumber_FromTheThird(int missionNumber, string flipped)
    {
        var deck = new ResistanceDefectorDeck { Cards = "CNNCN" };

        Assert.Equal(flipped.Select(x => x == 'C'), deck.Flips(missionNumber));
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(5, false)]
    [InlineData(6, false)]
    public void TheCardOfAMission_ChangesAllegiance_OnlyWhenItIsAChangeAllegiance(int missionNumber, bool expected)
    {
        var deck = new ResistanceDefectorDeck { Cards = "CNNNN" };

        Assert.Equal(expected, deck.ChangesAllegianceAt(missionNumber));
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void TheDefectors_AreSwitched_WhenAnOddNumberOfChangesWereFlipped(int missionNumber, bool expected)
    {
        var deck = new ResistanceDefectorDeck { Cards = "CNCNN" };

        Assert.Equal(expected, deck.Switched(missionNumber));
    }

    [Fact]
    public void Clearing_EmptiesTheDeck()
    {
        var deck = new ResistanceDefectorDeck { Cards = "CNNNC" };

        deck.Clear();

        Assert.Empty(deck.Cards);
        Assert.Empty(deck.Flips(5));
    }
}
