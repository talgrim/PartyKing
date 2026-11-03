using PartyKing.Domain.Entities.Games.Resistance;
using PartyKing.Domain.Events;
using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Application.Tests;

/// <summary>The events <see cref="ResistanceGame"/> raises, with no database.</summary>
public class ResistanceEventsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    private static ResistanceGame NewGame(ResistancePhase phase, bool started = true)
    {
        var game = new ResistanceGame
        {
            SessionId = Guid.NewGuid(),
            IsOpen = true,
            StartedAt = started ? Now : null,
            Phase = phase,
        };
        for (var i = 0; i < 5; i++)
        {
            game.Players.Add(new ResistancePlayer
            {
                PlayerId = Guid.NewGuid(),
                Order = i,
                Role = i < 2 ? ResistanceRole.Spy : ResistanceRole.Resistance,
                IsOnTeam = phase is ResistancePhase.Voting or ResistancePhase.PlayingMission && i is 0 or 2,
            });
        }

        game.LeaderPlayerId = game.Players[0].PlayerId;

        return game;
    }

    private static Type[] TypesOf(ResistanceGame game) => game.DrainEvents().Select(x => x.GetType()).ToArray();

    [Fact]
    public void Create_RaisesCreated()
    {
        var game = ResistanceGame.Create(Guid.NewGuid(), Now, ResistanceRules.Default());

        Assert.Equal(new ResistanceCreated(game.SessionId), Assert.Single(game.DrainEvents()));
    }

    [Fact]
    public void Start_RaisesTheFirstLeaderThenStarted()
    {
        var game = NewGame(ResistancePhase.SelectingTeam, started: false);

        game.Start(Now, new Random(1));

        var events = game.DrainEvents();
        Assert.Equal(
            [typeof(ResistanceLeaderChanged), typeof(ResistanceStarted), typeof(ResistanceChanged)],
            events.Select(x => x.GetType()));
        Assert.Equal(game.LeaderPlayerId, events.OfType<ResistanceLeaderChanged>().Single().LeaderPlayerId);
    }

    [Fact]
    public void Restart_RaisesRestarted()
    {
        var game = NewGame(ResistancePhase.GameOver);

        game.Restart();

        Assert.Equal(
            [typeof(ResistanceRestarted), typeof(ResistanceChanged)], game.DrainEvents().Select(x => x.GetType()));
    }

    [Fact]
    public void SelectTeam_RaisesTheSubmittedTeam()
    {
        var game = NewGame(ResistancePhase.SelectingTeam);
        var team = new[] { game.Players[0].PlayerId, game.Players[3].PlayerId };

        game.SelectTeam(game.LeaderPlayerId, team);

        IDomainEvent[] expected =
        [
            new ResistanceTeamSubmitted(game.SessionId, 1, game.LeaderPlayerId!.Value, team),
            new ResistanceChanged(game.SessionId),
        ];
        Assert.Equal(expected, game.DrainEvents(), new EventComparer());
    }

    [Fact]
    public void AVoteThatDoesNotCompleteTheRound_RaisesOnlyWhoVoted()
    {
        var game = NewGame(ResistancePhase.Voting);
        var voter = game.Players[3];

        game.Vote(voter.PlayerId, false);

        IDomainEvent[] expected = [new ResistanceVoteCast(game.SessionId, voter.PlayerId), new ResistanceChanged(game.SessionId)];
        Assert.Equal(expected, game.DrainEvents());
    }

    [Fact]
    public void TheLastApprovingVote_RaisesTheVoteThenTheApproval()
    {
        var game = NewGame(ResistancePhase.Voting);

        foreach (var player in game.Players)
        {
            game.Vote(player.PlayerId, true);
        }

        var events = game.DrainEvents();
        Assert.Equal(5, events.OfType<ResistanceVoteCast>().Count());
        Assert.Equal(
            [typeof(ResistanceVoteCast), typeof(ResistanceTeamApproved), typeof(ResistanceChanged)],
            events.Skip(events.Count - 3).Select(x => x.GetType()));
        Assert.Equal(new ResistanceTeamApproved(game.SessionId, 1), events.OfType<ResistanceTeamApproved>().Single());
    }

    [Fact]
    public void ARejection_RaisesTheRejectedCountAndTheNewLeader()
    {
        var game = NewGame(ResistancePhase.Voting);
        var next = game.Players[1].PlayerId;

        foreach (var player in game.Players)
        {
            game.Vote(player.PlayerId, false);
        }

        var events = game.DrainEvents();
        Assert.Equal(new ResistanceTeamRejected(game.SessionId, 1, 1), events.OfType<ResistanceTeamRejected>().Single());
        Assert.Equal(next, events.OfType<ResistanceLeaderChanged>().Single().LeaderPlayerId);
        Assert.DoesNotContain(events, x => x is ResistanceGameOver);
    }

    [Fact]
    public void TheFifthRejection_EndsTheGameForTheSpiesWithoutPassingTheLead()
    {
        var game = NewGame(ResistancePhase.Voting);
        game.RejectedTeams = 4;

        foreach (var player in game.Players)
        {
            game.Vote(player.PlayerId, false);
        }

        var events = game.DrainEvents();
        Assert.Equal(new ResistanceTeamRejected(game.SessionId, 1, 5), events.OfType<ResistanceTeamRejected>().Single());
        Assert.Equal(new ResistanceGameOver(game.SessionId, ResistanceRole.Spy), events.OfType<ResistanceGameOver>().Single());
        Assert.DoesNotContain(events, x => x is ResistanceLeaderChanged);
    }

    [Fact]
    public void AMissionCard_RaisesWhoPlayedItButNotWhichOne()
    {
        var game = NewGame(ResistancePhase.PlayingMission);
        var spy = game.Players[0];

        game.PlayMissionCard(spy.PlayerId, success: false);

        IDomainEvent[] expected = [new ResistanceMissionCardPlayed(game.SessionId, spy.PlayerId), new ResistanceChanged(game.SessionId)];
        Assert.Equal(expected, game.DrainEvents());
    }

    [Fact]
    public void TheLastMissionCard_RaisesTheCompletedMission()
    {
        var game = NewGame(ResistancePhase.PlayingMission);

        game.PlayMissionCard(game.Players[0].PlayerId, success: false);
        game.PlayMissionCard(game.Players[2].PlayerId, success: true);

        var completed = game.DrainEvents().OfType<ResistanceMissionCompleted>().Single();
        Assert.Equal(new ResistanceMissionCompleted(game.SessionId, 1, false, 1), completed);
    }

    [Fact]
    public void TheThirdSuccess_EndsTheGameForTheResistance()
    {
        var game = NewGame(ResistancePhase.PlayingMission);
        for (var mission = 1; mission <= 2; mission++)
        {
            game.MissionResults.Add(new ResistanceMissionResult { SessionId = game.SessionId, MissionNumber = mission, Succeeded = true });
        }

        game.MissionNumber = 3;
        game.PlayMissionCard(game.Players[0].PlayerId, success: true);
        game.PlayMissionCard(game.Players[2].PlayerId, success: true);

        var events = game.DrainEvents();
        Assert.Equal(new ResistanceGameOver(game.SessionId, ResistanceRole.Resistance), events.OfType<ResistanceGameOver>().Single());
    }

    [Fact]
    public void Continue_RaisesTheNewLeader()
    {
        var game = NewGame(ResistancePhase.MissionResult);

        game.Continue(game.LeaderPlayerId, isManager: false);

        Assert.Equal(game.Players[1].PlayerId, game.DrainEvents().OfType<ResistanceLeaderChanged>().Single().LeaderPlayerId);
    }

    [Fact]
    public void PlayingAPlotCard_RaisesItWithTheHolderAndTarget()
    {
        var game = NewGame(ResistancePhase.LeaderWindow);
        var holder = game.Players[1];
        game.PlotCards.Add(new ResistancePlotCard
        {
            SessionId = game.SessionId,
            CardId = 4,
            Type = ResistancePlotCardType.StrongLeader,
            Location = ResistancePlotCardLocation.Hand,
            HolderPlayerId = holder.PlayerId,
        });

        game.TakeLead(holder.PlayerId, 4, Now);

        var events = game.DrainEvents();
        Assert.Equal(
            new ResistancePlotCardPlayed(
                game.SessionId, 4, ResistancePlotCardType.StrongLeader, holder.PlayerId, game.Players[0].PlayerId),
            events.OfType<ResistancePlotCardPlayed>().Single());
        Assert.Equal(holder.PlayerId, events.OfType<ResistanceLeaderChanged>().Single().LeaderPlayerId);
    }

    [Fact]
    public void EveryActionThatHappened_RaisesTheCatchAll_AndARefusedOneRaisesNothing()
    {
        var game = NewGame(ResistancePhase.Voting);

        var refused = game.Vote(Guid.NewGuid(), true);
        Assert.NotNull(refused.Failure);
        Assert.Empty(game.DrainEvents());

        game.Vote(game.Players[0].PlayerId, true);
        Assert.Contains(typeof(ResistanceChanged), TypesOf(game));

        var closed = NewGame(ResistancePhase.SelectingTeam);
        closed.IsOpen = false;
        closed.StartedAt = null;
        closed.SetPlotCards(true);
        closed.RefreshRules(ResistanceRules.Default());
        Assert.Equal([typeof(ResistanceChanged), typeof(ResistanceChanged)], TypesOf(closed));
    }

    private sealed class EventComparer : IEqualityComparer<IDomainEvent>
    {
        // The submitted team is a list, which records compare by reference
        public bool Equals(IDomainEvent? x, IDomainEvent? y)
        {
            return x is ResistanceTeamSubmitted a && y is ResistanceTeamSubmitted b
                ? (a.SessionId, a.MissionNumber, a.LeaderPlayerId) == (b.SessionId, b.MissionNumber, b.LeaderPlayerId)
                  && a.TeamPlayerIds.SequenceEqual(b.TeamPlayerIds)
                : object.Equals(x, y);
        }

        public int GetHashCode(IDomainEvent obj) => obj.GetHashCode();
    }
}
