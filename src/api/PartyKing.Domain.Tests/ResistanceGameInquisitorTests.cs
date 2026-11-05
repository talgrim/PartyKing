using PartyKing.Domain.Entities.Games.Resistance;
using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Domain.Tests;

/// <summary>The Inquisitor module of <see cref="ResistanceGame"/>, with no database.</summary>
public class ResistanceGameInquisitorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 20, 0, 0, TimeSpan.Zero);

    private static ResistanceRules InquisitorRules()
    {
        var rules = ResistanceRules.Default();
        rules.InquisitorEnabled = true;

        return rules;
    }

    /// <summary>Five players, the module on, the token with the last one and a mission about to be completed.</summary>
    private static ResistanceGame MissionGame(int missionNumber = 2, bool inquisitor = true)
    {
        var game = new ResistanceGame
        {
            SessionId = Guid.NewGuid(),
            IsOpen = true,
            StartedAt = Now,
            Phase = ResistancePhase.PlayingMission,
            MissionNumber = missionNumber,
            Rules = inquisitor ? InquisitorRules() : ResistanceRules.Default(),
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
        if (inquisitor)
        {
            game.InquisitorPlayerId = game.Players[4].PlayerId;
            game.Players[4].WasInquisitor = true;
        }

        return game;
    }

    private static void CompleteMission(ResistanceGame game, bool success = true)
    {
        game.PlayMissionCard(game.Players[2].PlayerId, success: true);
        game.PlayMissionCard(game.Players[0].PlayerId, success);
    }

    private static ResistanceGame InquisitionGame()
    {
        var game = MissionGame();
        CompleteMission(game);

        return game;
    }

    [Fact]
    public void Start_WithTheModuleOn_GivesTheTokenToThePlayerBeforeTheLeader_WrappingAtTheFirstSeat()
    {
        var leaderSeats = new HashSet<int>();
        for (var seed = 0; seed < 40; seed++)
        {
            var game = new ResistanceGame { SessionId = Guid.NewGuid(), IsOpen = true, Rules = InquisitorRules() };
            for (var i = 0; i < 5; i++)
            {
                game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = i });
            }

            game.Start(Now, new Random(seed));

            var leader = game.Players.First(x => x.PlayerId == game.LeaderPlayerId).Order;
            var holder = game.Players.Single(x => x.PlayerId == game.InquisitorPlayerId);
            Assert.Equal((leader + 4) % 5, holder.Order);
            Assert.Equal([holder.PlayerId], game.Players.Where(x => x.WasInquisitor).Select(x => x.PlayerId));
            leaderSeats.Add(leader);
        }

        Assert.Contains(0, leaderSeats);
    }

    [Fact]
    public void Start_WithTheModuleOff_HasNoToken()
    {
        var game = new ResistanceGame { SessionId = Guid.NewGuid(), IsOpen = true };
        for (var i = 0; i < 5; i++)
        {
            game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = i });
        }

        game.Start(Now, new Random(3));

        Assert.Null(game.InquisitorPlayerId);
        Assert.DoesNotContain(game.Players, x => x.WasInquisitor);
    }

    [Theory]
    [InlineData(2, ResistancePhase.Inquisition)]
    [InlineData(3, ResistancePhase.Inquisition)]
    [InlineData(4, ResistancePhase.Inquisition)]
    [InlineData(1, ResistancePhase.MissionResult)]
    [InlineData(5, ResistancePhase.MissionResult)]
    public void AFinishedMission_OpensTheInquisitionOnlyAfterMissions2To4(int mission, ResistancePhase expected)
    {
        var game = MissionGame(mission);

        CompleteMission(game);

        Assert.Equal(expected, game.Phase);
    }

    [Fact]
    public void AFinishedMission_WithTheModuleOff_GoesStraightToTheResult()
    {
        var game = MissionGame(inquisitor: false);

        CompleteMission(game);

        Assert.Equal(ResistancePhase.MissionResult, game.Phase);
    }

    [Fact]
    public void AMissionThatEndsTheGame_HasNoInquisition()
    {
        var game = MissionGame(3);
        for (var i = 1; i <= 2; i++)
        {
            game.MissionResults.Add(new ResistanceMissionResult
            {
                SessionId = game.SessionId, MissionNumber = i, Succeeded = true, TeamPlayerIds = [],
            });
        }

        CompleteMission(game);

        Assert.Equal(ResistancePhase.GameOver, game.Phase);
    }

    [Fact]
    public void TheInquisition_FollowsThePeekWindow()
    {
        var game = MissionGame();
        game.Rules.PlotCardsEnabled = true;
        game.PlotCards.Add(new ResistancePlotCard
        {
            SessionId = game.SessionId,
            CardId = 0,
            Type = ResistancePlotCardType.NoConfidence,
            Location = ResistancePlotCardLocation.Hand,
            HolderPlayerId = game.Players[1].PlayerId,
        });
        CompleteMission(game);
        Assert.Equal(ResistancePhase.PeekWindow, game.Phase);

        ResistanceRoles.AfterWindow(game);

        Assert.Equal(ResistancePhase.Inquisition, game.Phase);
    }

    [Fact]
    public void Examine_PassesTheTokenAtOnceAndSaysOnlyWho()
    {
        var game = InquisitionGame();
        game.DrainEvents();
        var holder = game.Players[4];
        var target = game.Players[1];

        var outcome = game.Examine(holder.PlayerId, target.PlayerId);

        Assert.Null(outcome.Failure);
        Assert.Equal(target.PlayerId, game.InquisitorPlayerId);
        Assert.True(target.WasInquisitor);
        Assert.Equal(holder.PlayerId, game.InquisitionExaminerPlayerId);
        Assert.Equal(target.PlayerId, game.InquisitionExaminedPlayerId);
        Assert.Equal(ResistancePhase.Inquisition, game.Phase);
        var passed = game.DrainEvents().OfType<ResistanceInquisitorTokenPassed>().Single();
        Assert.Equal((holder.PlayerId, target.PlayerId), (passed.FromPlayerId, passed.ToPlayerId));
    }

    [Fact]
    public void Examine_RefusesEveryoneButTheHolderAndEveryTargetThatIsNotAllowed()
    {
        var game = InquisitionGame();
        var holder = game.Players[4];

        Assert.Equal(ResistanceFailure.NotInquisitor, game.Examine(null, game.Players[1].PlayerId).Failure);
        Assert.Equal(ResistanceFailure.NotInquisitor, game.Examine(game.Players[0].PlayerId, game.Players[1].PlayerId).Failure);
        Assert.Equal(ResistanceFailure.InvalidExamineTarget, game.Examine(holder.PlayerId, holder.PlayerId).Failure);
        Assert.Equal(ResistanceFailure.InvalidExamineTarget, game.Examine(holder.PlayerId, Guid.NewGuid()).Failure);
        Assert.Equal(holder.PlayerId, game.InquisitorPlayerId);
        Assert.Null(game.InquisitionExaminerPlayerId);
        Assert.DoesNotContain(game.Players.Where(x => x != holder), x => x.WasInquisitor);
    }

    [Fact]
    public void Examine_OutsideTheInquisition_IsRefused()
    {
        var game = MissionGame();

        Assert.Equal(ResistanceFailure.NotInquisition, game.Examine(game.Players[4].PlayerId, game.Players[1].PlayerId).Failure);
    }

    [Fact]
    public void Examine_ASecondTime_IsRefusedForTheExaminerAndTheNewHolderAlike()
    {
        var game = InquisitionGame();
        var holder = game.Players[4];
        game.Examine(holder.PlayerId, game.Players[1].PlayerId);

        Assert.Equal(ResistanceFailure.AlreadyExamined, game.Examine(holder.PlayerId, game.Players[2].PlayerId).Failure);
        Assert.Equal(ResistanceFailure.NotInquisitor, game.Examine(game.Players[1].PlayerId, game.Players[2].PlayerId).Failure);
        Assert.Equal(game.Players[1].PlayerId, game.InquisitorPlayerId);
    }

    [Fact]
    public void Confirm_BeforeExamining_IsRefused()
    {
        var game = InquisitionGame();

        Assert.Equal(ResistanceFailure.NotExamined, game.ConfirmInquisition(game.Players[4].PlayerId).Failure);
        Assert.Equal(ResistanceFailure.NotInquisitor, game.ConfirmInquisition(game.Players[1].PlayerId).Failure);
        Assert.Equal(ResistancePhase.Inquisition, game.Phase);
    }

    [Fact]
    public void Confirm_IsOnlyForTheExaminerNotTheExaminedPlayer()
    {
        var game = InquisitionGame();
        game.Examine(game.Players[4].PlayerId, game.Players[1].PlayerId);

        Assert.Equal(ResistanceFailure.NotInquisitor, game.ConfirmInquisition(game.Players[1].PlayerId).Failure);
        Assert.Equal(ResistanceFailure.NotInquisitor, game.ConfirmInquisition(null).Failure);
        Assert.Equal(ResistancePhase.Inquisition, game.Phase);
    }

    [Fact]
    public void Confirm_EndsTheInquisitionAndAllowsContinuing()
    {
        var game = InquisitionGame();
        Assert.Equal(ResistanceFailure.NothingToContinue, game.Continue(game.LeaderPlayerId, isManager: true).Failure);
        game.Examine(game.Players[4].PlayerId, game.Players[1].PlayerId);

        var outcome = game.ConfirmInquisition(game.Players[4].PlayerId);

        Assert.Null(outcome.Failure);
        Assert.Equal(ResistancePhase.MissionResult, game.Phase);
        Assert.Null(game.InquisitionExaminerPlayerId);
        Assert.Null(game.InquisitionExaminedPlayerId);
        Assert.Equal(game.Players[1].PlayerId, game.InquisitorPlayerId);
        Assert.Equal(ResistanceFailure.NotInquisition, game.ConfirmInquisition(game.Players[4].PlayerId).Failure);
        Assert.Null(game.Continue(game.LeaderPlayerId, isManager: true).Failure);
        Assert.Equal(3, game.MissionNumber);
    }

    [Fact]
    public void ANextInquisition_CannotExamineAnyoneWhoHeldTheTokenBefore()
    {
        var game = InquisitionGame();
        game.Examine(game.Players[4].PlayerId, game.Players[1].PlayerId);
        game.ConfirmInquisition(game.Players[4].PlayerId);
        game.Continue(game.LeaderPlayerId, isManager: true);
        foreach (var player in game.Players)
        {
            player.IsOnTeam = player.Order is 0 or 2;
        }

        game.Phase = ResistancePhase.PlayingMission;
        CompleteMission(game);
        var holder = game.Players[1];

        Assert.Equal(ResistancePhase.Inquisition, game.Phase);
        Assert.Equal(ResistanceFailure.InvalidExamineTarget, game.Examine(holder.PlayerId, game.Players[4].PlayerId).Failure);
        Assert.Null(game.Examine(holder.PlayerId, game.Players[3].PlayerId).Failure);
    }

    [Fact]
    public void TakeResponsibility_IsNotPlayableDuringTheInquisition()
    {
        var game = InquisitionGame();
        game.PlotCards.Add(new ResistancePlotCard
        {
            SessionId = game.SessionId,
            CardId = 0,
            Type = ResistancePlotCardType.TakeResponsibility,
            Location = ResistancePlotCardLocation.Hand,
            HolderPlayerId = game.Players[1].PlayerId,
        });

        var outcome = game.TakeResponsibility(
            game.Players[1].PlayerId, 0, game.Players[2].PlayerId, null, Now, new Random(1));

        Assert.Equal(ResistanceFailure.CardNotPlayableNow, outcome.Failure);
    }

    [Fact]
    public void ThePeeksOfTheMission_StayVisibleDuringTheInquisition()
    {
        var game = MissionGame();
        game.Rules.PlotCardsEnabled = true;
        var peeker = game.Players[1];
        game.PlotCards.Add(new ResistancePlotCard
        {
            SessionId = game.SessionId,
            CardId = 0,
            Type = ResistancePlotCardType.KeepingCloseEye,
            Location = ResistancePlotCardLocation.Played,
            HolderPlayerId = peeker.PlayerId,
            TargetPlayerId = game.Players[0].PlayerId,
            PlayedInMission = 2,
            IsConfirmed = true,
        });
        CompleteMission(game);
        Assert.Equal(ResistancePhase.Inquisition, game.Phase);

        var peeks = ResistanceRoles.PeekedCards(game, peeker.PlayerId).ToList();

        Assert.Equal([(game.Players[0].PlayerId, true)], peeks);
    }

    [Theory]
    [InlineData(ResistancePhase.PeekWindow, true)]
    [InlineData(ResistancePhase.Inquisition, true)]
    [InlineData(ResistancePhase.MissionResult, true)]
    [InlineData(ResistancePhase.GameOver, false)]
    public void TheMissionIsResolved_FromTheResultUntilTheGameMovesOn_InAnyPhaseBetween(ResistancePhase phase, bool expected)
    {
        var game = MissionGame();
        CompleteMission(game);
        game.Phase = phase;

        Assert.Equal(expected, game.IsMissionResolved);
    }

    [Theory]
    [InlineData(ResistancePhase.PlayingMission)]
    [InlineData(ResistancePhase.SelectingTeam)]
    public void TheMissionIsNotResolved_BeforeItsResultOrOnceTheNextOneStarted(ResistancePhase phase)
    {
        var game = MissionGame();
        game.Phase = phase;
        Assert.False(game.IsMissionResolved);

        CompleteMission(game);
        game.Phase = ResistancePhase.MissionResult;
        game.Continue(game.LeaderPlayerId, isManager: true);

        Assert.False(game.IsMissionResolved);
    }

    [Fact]
    public void Restart_ClearsTheTokenAndWhoHeldIt()
    {
        var game = InquisitionGame();
        game.Examine(game.Players[4].PlayerId, game.Players[1].PlayerId);
        game.Phase = ResistancePhase.GameOver;

        game.Restart();

        Assert.Null(game.InquisitorPlayerId);
        Assert.Null(game.InquisitionExaminerPlayerId);
        Assert.DoesNotContain(game.Players, x => x.WasInquisitor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetInquisitor_BeforeTheGameStarts_SwitchesTheModuleOnANewRulesObject(bool open)
    {
        var rules = ResistanceRules.Default();
        rules.PlotCardsEnabled = true;
        var game = new ResistanceGame { IsOpen = open, Rules = rules };

        var outcome = game.SetInquisitor(true);

        Assert.Null(outcome.Failure);
        Assert.True(game.Rules.InquisitorEnabled);
        Assert.True(game.Rules.PlotCardsEnabled);
        Assert.NotSame(rules, game.Rules);
        Assert.False(rules.InquisitorEnabled);
    }

    [Fact]
    public void SetInquisitor_OnceTheGameStarted_IsRefusedAndChangesNothing()
    {
        var rules = ResistanceRules.Default();
        var game = new ResistanceGame { StartedAt = Now, Rules = rules };

        var outcome = game.SetInquisitor(true);

        Assert.Equal(ResistanceFailure.InquisitorLocked, outcome.Failure);
        Assert.Same(rules, game.Rules);
        Assert.False(game.Rules.InquisitorEnabled);
    }

    [Fact]
    public void TheOtherSwitches_KeepTheInquisitorFlag()
    {
        var game = new ResistanceGame { Rules = InquisitorRules() };

        game.SetPlotCards(true);
        Assert.True(game.Rules.InquisitorEnabled);

        game.RefreshRules(ResistanceRules.Default());
        Assert.True(game.Rules.InquisitorEnabled);
        Assert.True(game.Rules.PlotCardsEnabled);
    }
}
