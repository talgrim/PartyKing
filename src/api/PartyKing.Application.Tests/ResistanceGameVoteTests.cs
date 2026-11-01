using PartyKing.Domain.Entities.Games;

namespace PartyKing.Application.Tests;

/// <summary>Voting on a submitted team, with no database.</summary>
public class ResistanceGameVoteTests
{
    private static ResistanceGame VotingGame(int playerCount = 5)
    {
        var game = new ResistanceGame
        {
            SessionId = Guid.NewGuid(),
            IsOpen = true,
            StartedAt = DateTimeOffset.UtcNow,
            Phase = ResistancePhase.Voting,
        };
        for (var i = 0; i < playerCount; i++)
        {
            game.Players.Add(new ResistancePlayer
            {
                PlayerId = Guid.NewGuid(),
                Order = i,
                Role = i < 2 ? ResistanceRole.Spy : ResistanceRole.Resistance,
                IsOnTeam = i < 2,
            });
        }

        game.LeaderPlayerId = game.Players[0].PlayerId;

        return game;
    }

    [Fact]
    public void AVote_IsStoredAndCanBeReplacedUntilTheLastPlayerVoted()
    {
        var game = VotingGame();
        var voter = game.Players[3];

        var first = game.Vote(voter.PlayerId, true);
        var second = game.Vote(voter.PlayerId, false);

        Assert.Null(first.Failure);
        Assert.Null(second.Failure);
        Assert.True(second.Changed);
        Assert.False(voter.Vote);
        Assert.Equal(ResistancePhase.Voting, game.Phase);
    }

    [Fact]
    public void TheLastVote_TalliesTheRoundInTheSameCall()
    {
        var game = VotingGame();
        foreach (var player in game.Players.Take(4))
        {
            game.Vote(player.PlayerId, true);
        }

        Assert.Equal(ResistancePhase.Voting, game.Phase);

        game.Vote(game.Players[4].PlayerId, true);

        Assert.Equal(ResistancePhase.PlayingMission, game.Phase);
    }

    [Fact]
    public void ATiedVote_RejectsTheTeam()
    {
        var game = VotingGame(6);
        for (var i = 0; i < 6; i++)
        {
            game.Vote(game.Players[i].PlayerId, i < 3);
        }

        Assert.Equal(ResistancePhase.SelectingTeam, game.Phase);
        Assert.Equal(1, game.RejectedTeams);
    }

    [Fact]
    public void TheGuards_AreCheckedInOrderAndChangeNothing()
    {
        var notStarted = VotingGame();
        notStarted.StartedAt = null;
        var over = VotingGame();
        over.Phase = ResistancePhase.GameOver;
        var selecting = VotingGame();
        selecting.Phase = ResistancePhase.SelectingTeam;
        var voting = VotingGame();

        Assert.Equal(ResistanceFailure.NotStarted, notStarted.Vote(notStarted.Players[0].PlayerId, true).Failure);
        Assert.Equal(ResistanceFailure.GameOver, over.Vote(over.Players[0].PlayerId, true).Failure);
        Assert.Equal(ResistanceFailure.NotPlayerToVote, voting.Vote(null, true).Failure);
        Assert.Equal(ResistanceFailure.NotPlayerToVote, voting.Vote(Guid.NewGuid(), true).Failure);
        Assert.Equal(ResistanceFailure.NotVoting, selecting.Vote(selecting.Players[0].PlayerId, true).Failure);
        Assert.All(voting.Players, x => Assert.Null(x.Vote));
        Assert.False(voting.Vote(null, true).Changed);
    }

    [Fact]
    public void AnOpinionMaker_VotesFirstAndCannotChangeTheVote()
    {
        var game = VotingGame();
        var maker = game.Players[2];
        var other = game.Players[3];
        game.PlotCards.Add(new ResistancePlotCard
        {
            CardId = 1,
            Type = ResistancePlotCardType.OpinionMaker,
            Location = ResistancePlotCardLocation.InPlay,
            HolderPlayerId = maker.PlayerId,
        });

        Assert.Equal(ResistanceFailure.OpinionMakersFirst, game.Vote(other.PlayerId, true).Failure);
        Assert.Null(game.Vote(maker.PlayerId, false).Failure);
        Assert.Equal(ResistanceFailure.VoteLocked, game.Vote(maker.PlayerId, true).Failure);
        Assert.Null(game.Vote(other.PlayerId, true).Failure);
    }
}
