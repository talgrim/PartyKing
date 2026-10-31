using PartyKing.Domain.Entities.Games;

namespace PartyKing.Application.Tests;

/// <summary>The rules that decide what a Resistance game shows to everybody.</summary>
public class ResistanceRolesViewRulesTests
{
    private static ResistanceGame NewGame(ResistancePhase phase, params bool?[] votes)
    {
        var game = new ResistanceGame { Phase = phase };
        for (var i = 0; i < votes.Length; i++)
        {
            game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = i, Vote = votes[i] });
        }

        return game;
    }

    private static ResistancePlotCard Card(
        ResistancePlotCardType type, ResistancePlotCardLocation location, Guid? holder = null) =>
        new() { Type = type, Location = location, HolderPlayerId = holder };

    // ---- VotesRevealed ----

    [Theory]
    [InlineData(ResistancePhase.Voting, false)]
    [InlineData(ResistancePhase.SelectingTeam, true)]
    [InlineData(ResistancePhase.PlayingMission, true)]
    [InlineData(ResistancePhase.MissionResult, true)]
    [InlineData(ResistancePhase.GameOver, true)]
    public void VotesRevealed_WhenEveryoneVoted_DependsOnTheVoteBeingOver(ResistancePhase phase, bool expected)
    {
        var game = NewGame(phase, true, false, true);

        Assert.Equal(expected, ResistanceRoles.VotesRevealed(game));
    }

    [Theory]
    [InlineData(ResistancePhase.Voting)]
    [InlineData(ResistancePhase.SelectingTeam)]
    [InlineData(ResistancePhase.PlayingMission)]
    public void VotesRevealed_WhenSomeoneHasNotVoted_IsFalse(ResistancePhase phase)
    {
        var game = NewGame(phase, true, null, true);

        Assert.False(ResistanceRoles.VotesRevealed(game));
    }

    // ---- IsConfirmationWindow ----

    [Fact]
    public void IsConfirmationWindow_IsTrueForExactlyTheThreeWindows()
    {
        var windows = new[] { ResistancePhase.LeaderWindow, ResistancePhase.ConfidenceWindow, ResistancePhase.PeekWindow };

        foreach (var phase in Enum.GetValues<ResistancePhase>())
        {
            Assert.Equal(windows.Contains(phase), ResistanceRoles.IsConfirmationWindow(NewGame(phase)));
        }
    }

    // ---- NeedsDealing ----

    [Fact]
    public void NeedsDealing_IsTrueOnlyForDrawnCardsThatAreNotEstablishConfidence()
    {
        foreach (var type in Enum.GetValues<ResistancePlotCardType>())
        {
            foreach (var location in Enum.GetValues<ResistancePlotCardLocation>())
            {
                var expected = location == ResistancePlotCardLocation.Drawn
                               && type != ResistancePlotCardType.EstablishConfidence;

                Assert.Equal(expected, ResistanceRoles.NeedsDealing(Card(type, location)));
            }
        }
    }

    [Fact]
    public void CardsToDeal_ListsTheCardsThatNeedDealing()
    {
        var game = NewGame(ResistancePhase.DealingCards);
        var toDeal = Card(ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.Drawn);
        game.PlotCards.AddRange(
        [
            toDeal,
            Card(ResistancePlotCardType.EstablishConfidence, ResistancePlotCardLocation.Drawn),
            Card(ResistancePlotCardType.OpenUp, ResistancePlotCardLocation.Hand)
        ]);

        Assert.Equal([toDeal], ResistanceRoles.CardsToDeal(game));
    }

    // ---- OpinionMakerVotesRevealed ----

    [Fact]
    public void OpinionMakerVotesRevealed_WithoutAnOpinionMaker_IsFalse()
    {
        var game = NewGame(ResistancePhase.Voting, true, true, true);

        Assert.False(ResistanceRoles.OpinionMakerVotesRevealed(game));
    }

    [Fact]
    public void OpinionMakerVotesRevealed_WhenAHolderHasNotVoted_IsFalse()
    {
        var game = NewGame(ResistancePhase.Voting, true, null, null);
        game.PlotCards.Add(Card(ResistancePlotCardType.OpinionMaker, ResistancePlotCardLocation.InPlay, game.Players[0].PlayerId));
        game.PlotCards.Add(Card(ResistancePlotCardType.OpinionMaker, ResistancePlotCardLocation.InPlay, game.Players[1].PlayerId));

        Assert.False(ResistanceRoles.OpinionMakerVotesRevealed(game));
    }

    [Fact]
    public void OpinionMakerVotesRevealed_WhenEveryHolderVoted_IsTrueEvenIfOthersHaveNot()
    {
        var game = NewGame(ResistancePhase.Voting, true, false, null);
        game.PlotCards.Add(Card(ResistancePlotCardType.OpinionMaker, ResistancePlotCardLocation.InPlay, game.Players[0].PlayerId));
        game.PlotCards.Add(Card(ResistancePlotCardType.OpinionMaker, ResistancePlotCardLocation.InPlay, game.Players[1].PlayerId));

        Assert.True(ResistanceRoles.OpinionMakerVotesRevealed(game));
    }

    [Fact]
    public void OpinionMakerVotesRevealed_IgnoresAnOpinionMakerThatIsNotInPlay()
    {
        var game = NewGame(ResistancePhase.Voting, true, null);
        game.PlotCards.Add(Card(ResistancePlotCardType.OpinionMaker, ResistancePlotCardLocation.Hand, game.Players[1].PlayerId));

        Assert.False(ResistanceRoles.OpinionMakerVotesRevealed(game));
    }
}
