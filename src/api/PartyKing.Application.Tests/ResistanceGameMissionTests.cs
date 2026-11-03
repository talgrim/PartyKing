using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Application.Tests;

/// <summary>The mission cards and moving on of <see cref="ResistanceGame"/>, with no database.</summary>
public class ResistanceGameMissionTests
{
    private static ResistanceGame MissionGame(ResistancePhase phase = ResistancePhase.PlayingMission)
    {
        var game = new ResistanceGame
        {
            SessionId = Guid.NewGuid(),
            IsOpen = true,
            StartedAt = DateTimeOffset.UtcNow,
            Phase = phase,
        };
        for (var i = 0; i < 5; i++)
        {
            game.Players.Add(new ResistancePlayer
            {
                PlayerId = Guid.NewGuid(),
                Order = i,
                Role = i < 2 ? ResistanceRole.Spy : ResistanceRole.Resistance,
                IsOnTeam = i is 0 or 2,
            });
        }

        game.LeaderPlayerId = game.Players[0].PlayerId;

        return game;
    }

    [Fact]
    public void ACardOfATeamMember_IsStoredAndTheMissionWaitsForTheOthers()
    {
        var game = MissionGame();
        var spy = game.Players[0];

        var outcome = game.PlayMissionCard(spy.PlayerId, success: false);

        Assert.Null(outcome.Failure);
        Assert.True(outcome.Changed);
        Assert.False(spy.MissionCard);
        Assert.Equal(ResistancePhase.PlayingMission, game.Phase);
        Assert.Empty(game.MissionResults);
    }

    [Fact]
    public void TheLastCard_ResolvesTheMissionInTheSameCall()
    {
        var game = MissionGame();

        game.PlayMissionCard(game.Players[2].PlayerId, success: true);
        game.PlayMissionCard(game.Players[0].PlayerId, success: false);

        var result = Assert.Single(game.MissionResults);
        Assert.False(result.Succeeded);
        Assert.NotEqual(ResistancePhase.PlayingMission, game.Phase);
    }

    [Fact]
    public void TheGuards_AreCheckedInOrderAndChangeNothing()
    {
        var notStarted = MissionGame();
        notStarted.StartedAt = null;
        var over = MissionGame(ResistancePhase.GameOver);
        var voting = MissionGame(ResistancePhase.Voting);
        var game = MissionGame();
        var spy = game.Players[0];
        var offTeam = game.Players[1];
        var resistance = game.Players[2];

        Assert.Equal(ResistanceFailure.NotStarted, notStarted.PlayMissionCard(notStarted.Players[0].PlayerId, true).Failure);
        Assert.Equal(ResistanceFailure.GameOver, over.PlayMissionCard(over.Players[0].PlayerId, true).Failure);
        Assert.Equal(ResistanceFailure.NotPlayerToPlay, game.PlayMissionCard(null, true).Failure);
        Assert.Equal(ResistanceFailure.NotPlayerToPlay, game.PlayMissionCard(Guid.NewGuid(), true).Failure);
        Assert.Equal(ResistanceFailure.NoApprovedTeam, voting.PlayMissionCard(voting.Players[0].PlayerId, true).Failure);
        Assert.Equal(ResistanceFailure.NotOnTeam, game.PlayMissionCard(offTeam.PlayerId, true).Failure);
        Assert.Equal(ResistanceFailure.MustSucceed, game.PlayMissionCard(resistance.PlayerId, false).Failure);
        Assert.All(game.Players, x => Assert.Null(x.MissionCard));
        Assert.False(game.PlayMissionCard(offTeam.PlayerId, true).Changed);
    }

    [Fact]
    public void ACardCannotBePlayedTwice()
    {
        var game = MissionGame();
        var spy = game.Players[0];
        game.PlayMissionCard(spy.PlayerId, success: true);

        var again = game.PlayMissionCard(spy.PlayerId, success: false);

        Assert.Equal(ResistanceFailure.AlreadyPlayed, again.Failure);
        Assert.True(spy.MissionCard);
    }

    [Fact]
    public void Continue_AfterAFinishedMission_PassesTheLeadership()
    {
        var game = MissionGame(ResistancePhase.MissionResult);
        var leader = game.LeaderPlayerId;

        var outcome = game.Continue(leader, isManager: false);

        Assert.Null(outcome.Failure);
        Assert.True(outcome.Changed);
        Assert.NotEqual(leader, game.LeaderPlayerId);
        Assert.NotEqual(ResistancePhase.MissionResult, game.Phase);
    }

    [Fact]
    public void Continue_IsForTheLeaderOrAManager()
    {
        var game = MissionGame(ResistancePhase.MissionResult);
        var other = game.Players[3].PlayerId;

        Assert.Equal(ResistanceFailure.NotAllowedToContinue, game.Continue(other, isManager: false).Failure);
        Assert.Equal(ResistanceFailure.NotAllowedToContinue, game.Continue(null, isManager: false).Failure);
        Assert.Null(game.Continue(null, isManager: true).Failure);
    }

    [Fact]
    public void Continue_OutsideAFinishedMission_IsRefusedAndChangesNothing()
    {
        var game = MissionGame(ResistancePhase.Voting);
        var notStarted = MissionGame(ResistancePhase.MissionResult);
        notStarted.StartedAt = null;
        var over = MissionGame(ResistancePhase.GameOver);

        var outcome = game.Continue(null, isManager: true);

        Assert.Equal(ResistanceFailure.NothingToContinue, outcome.Failure);
        Assert.False(outcome.Changed);
        Assert.Equal(ResistanceFailure.NotStarted, notStarted.Continue(null, true).Failure);
        Assert.Equal(ResistanceFailure.GameOver, over.Continue(null, true).Failure);
    }
}
