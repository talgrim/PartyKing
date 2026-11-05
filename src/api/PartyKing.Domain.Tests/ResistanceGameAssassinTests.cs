using PartyKing.Domain.Entities.Games.Resistance;
using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Domain.Tests;

/// <summary>The Assassin module of <see cref="ResistanceGame"/>, with no database.</summary>
public class ResistanceGameAssassinTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 21, 0, 0, TimeSpan.Zero);

    private static ResistanceRules AssassinRules()
    {
        var rules = ResistanceRules.Default();
        rules.AssassinEnabled = true;

        return rules;
    }

    private static ResistanceGame LobbyGame(ResistanceRules? rules = null)
    {
        var game = new ResistanceGame { SessionId = Guid.NewGuid(), IsOpen = true, Rules = rules ?? AssassinRules() };
        for (var i = 0; i < 5; i++)
        {
            game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = i });
        }

        return game;
    }

    /// <summary>
    /// Five players in the last mission: seats 0 (Assassin) and 1 (spy), seat 2 the Commander, seats 3 and 4 standard
    /// resistance members. Two missions are won; the team of the third is seats 2 and 3.
    /// </summary>
    private static ResistanceGame MissionGame(int successes = 2, int failures = 0)
    {
        var game = new ResistanceGame
        {
            SessionId = Guid.NewGuid(),
            IsOpen = true,
            StartedAt = Now,
            Phase = ResistancePhase.PlayingMission,
            MissionNumber = successes + failures + 1,
            Rules = AssassinRules(),
        };
        for (var i = 0; i < 5; i++)
        {
            game.Players.Add(new ResistancePlayer
            {
                PlayerId = Guid.NewGuid(),
                Order = i,
                Role = i < 2 ? ResistanceRole.Spy : ResistanceRole.Resistance,
                Character = i switch
                {
                    0 => ResistanceCharacter.Assassin,
                    2 => ResistanceCharacter.Commander,
                    _ => ResistanceCharacter.Standard,
                },
                IsOnTeam = i is 2 or 3,
            });
        }

        game.LeaderPlayerId = game.Players[2].PlayerId;
        for (var i = 1; i <= successes + failures; i++)
        {
            game.MissionResults.Add(new ResistanceMissionResult
            {
                SessionId = game.SessionId, MissionNumber = i, Succeeded = i <= successes,
            });
        }

        return game;
    }

    private static ResistanceGame AssassinationGame()
    {
        var game = MissionGame();
        game.PlayMissionCard(game.Players[2].PlayerId, ResistanceMissionCardKind.Success);
        game.PlayMissionCard(game.Players[3].PlayerId, ResistanceMissionCardKind.Success);
        game.DrainEvents();

        return game;
    }

    [Fact]
    public void Start_WithTheModuleOn_DealsOneAssassinAmongTheSpiesAndOneCommanderAmongTheResistance()
    {
        for (var seed = 0; seed < 30; seed++)
        {
            var game = LobbyGame();

            game.Start(Now, new Random(seed));

            var assassin = Assert.Single(game.Players, x => x.Character == ResistanceCharacter.Assassin);
            var commander = Assert.Single(game.Players, x => x.Character == ResistanceCharacter.Commander);
            Assert.Equal(ResistanceRole.Spy, assassin.Role);
            Assert.Equal(ResistanceRole.Resistance, commander.Role);
        }
    }

    [Fact]
    public void Start_WithoutTheModule_DealsNoSpecialCharacterAndTheSameRolesAsBefore()
    {
        var withModule = LobbyGame();
        var without = LobbyGame(ResistanceRules.Default());
        for (var i = 0; i < 5; i++)
        {
            without.Players[i].PlayerId = withModule.Players[i].PlayerId;
        }

        withModule.Start(Now, new Random(7));
        without.Start(Now, new Random(7));

        Assert.All(without.Players, x => Assert.Equal(ResistanceCharacter.Standard, x.Character));
        Assert.Equal(withModule.Players.Select(x => x.Role), without.Players.Select(x => x.Role));
        Assert.Equal(withModule.LeaderPlayerId, without.LeaderPlayerId);
    }

    [Fact]
    public void SetAssassin_BeforeTheStart_SwitchesTheModule()
    {
        var game = LobbyGame(ResistanceRules.Default());

        var outcome = game.SetAssassin(true);

        Assert.True(outcome.Changed);
        Assert.True(game.Rules.AssassinEnabled);
        Assert.True(game.SetAssassin(false).Changed);
        Assert.False(game.Rules.AssassinEnabled);
    }

    [Fact]
    public void SetAssassin_AfterTheStart_IsLocked()
    {
        var game = LobbyGame();
        game.Start(Now, new Random(1));

        var outcome = game.SetAssassin(false);

        Assert.Equal(ResistanceFailure.AssassinLocked, outcome.Failure);
    }

    [Theory]
    [InlineData("defector")]
    [InlineData("reverser")]
    [InlineData("blind")]
    public void SetAssassin_WhileAnIncompatibleModuleIsOn_IsRefused(string module)
    {
        var game = LobbyGame(ResistanceRules.Default());
        _ = module switch
        {
            "defector" => game.SetDefector(true),
            "reverser" => game.SetReverser(true),
            _ => game.SetBlindSpies(true),
        };

        var outcome = game.SetAssassin(true);

        Assert.Equal(ResistanceFailure.AssassinConflict, outcome.Failure);
        Assert.False(game.Rules.AssassinEnabled);
    }

    [Fact]
    public void TheIncompatibleModules_WhileTheAssassinIsOn_AreRefused()
    {
        var game = LobbyGame();

        Assert.Equal(ResistanceFailure.AssassinConflict, game.SetDefector(true).Failure);
        Assert.Equal(ResistanceFailure.AssassinConflict, game.SetReverser(true).Failure);
        Assert.Equal(ResistanceFailure.AssassinConflict, game.SetBlindSpies(true).Failure);
        Assert.True(game.SetDefector(false).Changed);
        Assert.True(game.SetReverser(false).Changed);
        Assert.True(game.SetBlindSpies(false).Changed);
    }

    [Fact]
    public void RefreshRules_KeepsTheAssassinFlag()
    {
        var game = LobbyGame();
        game.IsOpen = false;

        game.RefreshRules(ResistanceRules.Default());

        Assert.True(game.Rules.AssassinEnabled);
    }

    [Fact]
    public void TheThirdSuccess_OpensTheAssassinationInsteadOfEndingTheGame()
    {
        var game = MissionGame();

        game.PlayMissionCard(game.Players[2].PlayerId, ResistanceMissionCardKind.Success);
        game.PlayMissionCard(game.Players[3].PlayerId, ResistanceMissionCardKind.Success);

        Assert.Equal(ResistancePhase.Assassination, game.Phase);
        Assert.Null(game.Winner);
        Assert.Equal(3, game.ResistanceScore);
        var events = game.DrainEvents();
        Assert.DoesNotContain(events, x => x is ResistanceGameOver);
        Assert.Contains(events, x => x is ResistanceMissionCompleted { Succeeded: true });
    }

    [Fact]
    public void TheThirdSuccess_WithoutTheModule_StillEndsTheGame()
    {
        var game = MissionGame();
        game.Rules = ResistanceRules.Default();

        game.PlayMissionCard(game.Players[2].PlayerId, ResistanceMissionCardKind.Success);
        game.PlayMissionCard(game.Players[3].PlayerId, ResistanceMissionCardKind.Success);

        Assert.Equal(ResistancePhase.GameOver, game.Phase);
        Assert.Equal(ResistanceRole.Resistance, game.Winner);
    }

    [Fact]
    public void TheThirdFailure_WithTheModule_StillGivesTheSpiesTheWin()
    {
        var game = MissionGame(successes: 0, failures: 2);

        game.PlayMissionCard(game.Players[2].PlayerId, ResistanceMissionCardKind.Success);
        game.PlayMissionCard(game.Players[3].PlayerId, ResistanceMissionCardKind.Success);
        Assert.Equal(ResistancePhase.MissionResult, game.Phase);

        var failing = MissionGame(successes: 1, failures: 2);
        failing.Players[3].Role = ResistanceRole.Spy;
        failing.PlayMissionCard(failing.Players[2].PlayerId, ResistanceMissionCardKind.Success);
        failing.PlayMissionCard(failing.Players[3].PlayerId, ResistanceMissionCardKind.Fail);

        Assert.Equal(ResistancePhase.GameOver, failing.Phase);
        Assert.Equal(ResistanceRole.Spy, failing.Winner);
    }

    [Fact]
    public void Assassinate_NamingTheCommander_GivesTheSpiesTheWin()
    {
        var game = AssassinationGame();
        var assassin = game.Players[0];
        var commander = game.Players[2];

        var outcome = game.Assassinate(assassin.PlayerId, commander.PlayerId);

        Assert.True(outcome.Changed);
        Assert.Equal(ResistancePhase.GameOver, game.Phase);
        Assert.Equal(ResistanceRole.Spy, game.Winner);
        Assert.Equal(commander.PlayerId, game.AssassinationTargetPlayerId);
        var events = game.DrainEvents();
        Assert.Contains(new ResistanceAssassinated(game.SessionId, assassin.PlayerId, commander.PlayerId), events);
        Assert.Contains(new ResistanceGameOver(game.SessionId, ResistanceRole.Spy), events);
    }

    [Fact]
    public void Assassinate_NamingAnyoneElse_GivesTheResistanceTheWin()
    {
        var game = AssassinationGame();

        var outcome = game.Assassinate(game.Players[0].PlayerId, game.Players[3].PlayerId);

        Assert.True(outcome.Changed);
        Assert.Equal(ResistanceRole.Resistance, game.Winner);
        Assert.Equal(game.Players[3].PlayerId, game.AssassinationTargetPlayerId);
    }

    [Fact]
    public void Assassinate_Refusals_LeaveTheGameWaiting()
    {
        var game = AssassinationGame();
        var assassin = game.Players[0].PlayerId;

        Assert.Equal(ResistanceFailure.NotAssassin, game.Assassinate(game.Players[1].PlayerId, game.Players[2].PlayerId).Failure);
        Assert.Equal(ResistanceFailure.NotAssassin, game.Assassinate(null, game.Players[2].PlayerId).Failure);
        Assert.Equal(ResistanceFailure.NotAssassin, game.Assassinate(game.Players[2].PlayerId, game.Players[2].PlayerId).Failure);
        Assert.Equal(ResistanceFailure.InvalidAssassinationTarget, game.Assassinate(assassin, assassin).Failure);
        Assert.Equal(ResistanceFailure.InvalidAssassinationTarget, game.Assassinate(assassin, Guid.NewGuid()).Failure);
        Assert.Equal(ResistancePhase.Assassination, game.Phase);
        Assert.Null(game.Winner);
        Assert.Null(game.AssassinationTargetPlayerId);
    }

    [Fact]
    public void Assassinate_OutsideTheAssassination_IsRefused()
    {
        var game = MissionGame();

        var outcome = game.Assassinate(game.Players[0].PlayerId, game.Players[2].PlayerId);

        Assert.Equal(ResistanceFailure.NotAssassination, outcome.Failure);
    }

    [Fact]
    public void Assassinate_TwiceOrAfterTheGameIsOver_IsRefused()
    {
        var game = AssassinationGame();
        game.Assassinate(game.Players[0].PlayerId, game.Players[3].PlayerId);

        var outcome = game.Assassinate(game.Players[0].PlayerId, game.Players[2].PlayerId);

        Assert.Equal(ResistanceFailure.GameOver, outcome.Failure);
        Assert.Equal(ResistanceRole.Resistance, game.Winner);
    }

    [Fact]
    public void InTheAssassination_NoOtherActionOfTheRoundIsAllowed()
    {
        var game = AssassinationGame();
        var leader = game.Players[2].PlayerId;

        Assert.NotNull(game.SelectTeam(leader, [game.Players[2].PlayerId, game.Players[3].PlayerId, game.Players[4].PlayerId]).Failure);
        Assert.NotNull(game.Vote(game.Players[3].PlayerId, true).Failure);
        Assert.NotNull(game.PlayMissionCard(game.Players[3].PlayerId, ResistanceMissionCardKind.Success).Failure);
        Assert.NotNull(game.Continue(leader, isManager: true).Failure);
        Assert.Equal(ResistancePhase.Assassination, game.Phase);
    }

    [Fact]
    public void Restart_FromTheAssassination_ClearsTheGame()
    {
        var game = AssassinationGame();

        var outcome = game.Restart();

        Assert.True(outcome.Changed);
        Assert.Equal(ResistancePhase.SelectingTeam, game.Phase);
        Assert.Null(game.Winner);
        Assert.Empty(game.MissionResults);
        Assert.All(game.Players, x => Assert.Equal(ResistanceCharacter.Standard, x.Character));
    }

    [Fact]
    public void Restart_AfterTheGuess_ClearsTheTarget()
    {
        var game = AssassinationGame();
        game.Assassinate(game.Players[0].PlayerId, game.Players[3].PlayerId);

        game.Restart();

        Assert.Null(game.AssassinationTargetPlayerId);
    }

    [Fact]
    public void Restart_DuringAnOrdinaryMission_IsStillRefused()
    {
        var game = MissionGame();

        Assert.Equal(ResistanceFailure.NotOver, game.Restart().Failure);
    }

    [Fact]
    public void KnowsSpy_EveryViewerAndTarget()
    {
        var game = MissionGame();
        var players = game.Players;

        // The Commander (seat 2) knows both spies; the Assassin (seat 0) and the other spy know each other only;
        // the standard resistance members know nobody; nobody knows the Commander.
        bool[][] expected =
        [
            [false, true, false, false, false],
            [true, false, false, false, false],
            [true, true, false, false, false],
            [false, false, false, false, false],
            [false, false, false, false, false],
        ];
        for (var viewer = 0; viewer < 5; viewer++)
        {
            for (var target = 0; target < 5; target++)
            {
                Assert.Equal(
                    expected[viewer][target], ResistanceKnowledge.KnowsSpy(game, players[viewer], players[target]));
            }
        }
    }
}
