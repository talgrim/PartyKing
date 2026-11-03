using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Application.Tests;

/// <summary>The leader's team selection of <see cref="ResistanceGame"/>, with no database.</summary>
public class ResistanceGameTeamTests
{
    private static ResistanceGame StartedGame(int playerCount = 5, ResistancePhase phase = ResistancePhase.SelectingTeam)
    {
        var game = new ResistanceGame
        {
            SessionId = Guid.NewGuid(),
            IsOpen = true,
            StartedAt = DateTimeOffset.UtcNow,
            Phase = phase,
        };
        for (var i = 0; i < playerCount; i++)
        {
            game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = i });
        }

        game.LeaderPlayerId = game.Players[0].PlayerId;

        return game;
    }

    private static List<Guid> Ids(ResistanceGame game, int count) =>
        game.Players.Take(count).Select(x => x.PlayerId).ToList();

    [Fact]
    public void TheLeader_SubmitsATeamOfTheRequiredSize_AndTheGameGoesToVoting()
    {
        var game = StartedGame();
        game.Players[3].Vote = true;
        game.Players[3].MissionCard = true;
        var team = Ids(game, 2);

        var outcome = game.SelectTeam(game.LeaderPlayerId, team);

        Assert.Null(outcome.Failure);
        Assert.True(outcome.Changed);
        Assert.Equal(ResistancePhase.Voting, game.Phase);
        Assert.Equal(team, game.Players.Where(x => x.IsOnTeam).Select(x => x.PlayerId));
        Assert.All(game.Players, x =>
        {
            Assert.Null(x.Vote);
            Assert.Null(x.MissionCard);
        });
    }

    [Fact]
    public void TheTeamSize_FollowsTheMissionAndThePlayerCount()
    {
        var game = StartedGame(6);
        game.MissionNumber = 3;

        Assert.Equal(ResistanceFailure.WrongTeamSize, game.SelectTeam(game.LeaderPlayerId, Ids(game, 2)).Failure);
        Assert.Null(game.SelectTeam(game.LeaderPlayerId, Ids(game, 4)).Failure);
    }

    [Fact]
    public void ADuplicatedOrUnknownPlayer_IsRefused()
    {
        var game = StartedGame();
        var first = game.Players[0].PlayerId;

        Assert.Equal(ResistanceFailure.WrongTeamSize, game.SelectTeam(game.LeaderPlayerId, [first, first]).Failure);
        Assert.Equal(
            ResistanceFailure.UnknownTeamPlayer,
            game.SelectTeam(game.LeaderPlayerId, [first, Guid.NewGuid()]).Failure);
        Assert.Equal(ResistancePhase.SelectingTeam, game.Phase);
        Assert.All(game.Players, x => Assert.False(x.IsOnTeam));
    }

    [Fact]
    public void OnlyTheLeader_PicksTheTeam_AndAHostWithoutAPlayerNeverDoes()
    {
        var game = StartedGame();

        Assert.Equal(
            ResistanceFailure.NotLeaderToPick, game.SelectTeam(game.Players[1].PlayerId, Ids(game, 2)).Failure);
        Assert.Equal(ResistanceFailure.NotLeaderToPick, game.SelectTeam(null, Ids(game, 2)).Failure);
        Assert.False(game.SelectTeam(null, Ids(game, 2)).Changed);
    }

    [Theory]
    [InlineData(ResistancePhase.DealingCards)]
    [InlineData(ResistancePhase.ResolvingCards)]
    public void WhileThePlotCardsAreStillBeingDealt_TheTeamWaits(ResistancePhase phase)
    {
        var game = StartedGame(phase: phase);

        Assert.Equal(ResistanceFailure.CardsNotDealt, game.SelectTeam(game.LeaderPlayerId, Ids(game, 2)).Failure);
    }

    [Theory]
    [InlineData(ResistancePhase.Voting)]
    [InlineData(ResistancePhase.PlayingMission)]
    public void AfterTheTeamWasSubmitted_AnotherOneIsRefused(ResistancePhase phase)
    {
        var game = StartedGame(phase: phase);

        Assert.Equal(ResistanceFailure.TeamSubmitted, game.SelectTeam(game.LeaderPlayerId, Ids(game, 2)).Failure);
    }

    [Fact]
    public void InTheLeaderWindow_NoTeamExistsYet_SoTheRefusalDoesNotClaimOne()
    {
        var game = StartedGame(phase: ResistancePhase.LeaderWindow);

        Assert.Equal(ResistanceFailure.NotSelectingTeam, game.SelectTeam(game.LeaderPlayerId, Ids(game, 2)).Failure);
    }

    [Fact]
    public void BeforeTheStartAndAfterTheEnd_NobodyPicks()
    {
        var lobby = StartedGame();
        lobby.StartedAt = null;
        var over = StartedGame(phase: ResistancePhase.GameOver);

        Assert.Equal(ResistanceFailure.NotStarted, lobby.SelectTeam(lobby.LeaderPlayerId, Ids(lobby, 2)).Failure);
        Assert.Equal(ResistanceFailure.GameOver, over.SelectTeam(over.LeaderPlayerId, Ids(over, 2)).Failure);
    }
}
