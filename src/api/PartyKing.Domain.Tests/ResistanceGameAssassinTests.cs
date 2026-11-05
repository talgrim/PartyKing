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

    // ---- optional characters ----

    private static ResistanceGame LobbyOf(int players, Action<ResistanceRules> configure)
    {
        var rules = AssassinRules();
        configure(rules);
        var game = new ResistanceGame { SessionId = Guid.NewGuid(), IsOpen = true, Rules = rules };
        for (var i = 0; i < players; i++)
        {
            game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = i });
        }

        return game;
    }

    /// <summary>A started game with the given characters in the seats (everyone else standard); the first <paramref name="spies"/> seats are spies.</summary>
    private static ResistanceGame Table(int spies, params ResistanceCharacter[] characters)
    {
        var game = new ResistanceGame { SessionId = Guid.NewGuid(), IsOpen = true, StartedAt = Now, Rules = AssassinRules() };
        for (var i = 0; i < 7; i++)
        {
            game.Players.Add(new ResistancePlayer
            {
                PlayerId = Guid.NewGuid(),
                Order = i,
                Role = i < spies ? ResistanceRole.Spy : ResistanceRole.Resistance,
                Character = i < characters.Length ? characters[i] : ResistanceCharacter.Standard,
            });
        }

        return game;
    }

    [Fact]
    public void SetAssassin_WithOptions_SwitchesThemAndKeepsTheOmittedOnes()
    {
        var game = LobbyGame(ResistanceRules.Default());

        game.SetAssassin(true, bodyGuard: true, deepCover: true);
        game.SetAssassin(true, blindSpy: true);

        Assert.True(game.Rules.AssassinEnabled);
        Assert.True(game.Rules.BodyGuardEnabled);
        Assert.True(game.Rules.DeepCoverEnabled);
        Assert.True(game.Rules.BlindSpyEnabled);
        Assert.False(game.Rules.FalseCommanderEnabled);
    }

    [Fact]
    public void SetAssassin_Off_SwitchesTheOptionsOffToo()
    {
        var game = LobbyGame(ResistanceRules.Default());
        game.SetAssassin(true, bodyGuard: true, deepCover: true, blindSpy: true, falseCommander: true);

        game.SetAssassin(false);

        Assert.False(game.Rules.AssassinEnabled);
        Assert.False(game.Rules.BodyGuardEnabled);
        Assert.False(game.Rules.DeepCoverEnabled);
        Assert.False(game.Rules.BlindSpyEnabled);
        Assert.False(game.Rules.FalseCommanderEnabled);
    }

    [Fact]
    public void RefreshRules_KeepsTheOptionalCharacters()
    {
        var game = LobbyGame();
        game.SetAssassin(true, bodyGuard: true, falseCommander: true);
        game.IsOpen = false;

        game.RefreshRules(ResistanceRules.Default());

        Assert.True(game.Rules.BodyGuardEnabled);
        Assert.True(game.Rules.FalseCommanderEnabled);
    }

    [Theory]
    [InlineData(5, true, true, false)]
    [InlineData(6, false, true, true)]
    [InlineData(7, true, true, true)]
    [InlineData(9, true, true, true)]
    public void Start_WithMoreSpyCharactersThanSpySeatsLeftByTheAssassin_IsRefused(int players, bool deepCover, bool blindSpy, bool falseCommander)
    {
        var game = LobbyOf(players, rules =>
        {
            rules.DeepCoverEnabled = deepCover;
            rules.BlindSpyEnabled = blindSpy;
            rules.FalseCommanderEnabled = falseCommander;
        });

        var outcome = game.Start(Now, new Random(1));

        Assert.Equal(ResistanceFailure.TooManySpyCharacters, outcome.Failure);
        Assert.False(game.IsStarted);
        Assert.All(game.Players, x => Assert.Null(x.Role));
    }

    [Theory]
    [InlineData(5, false, false)]
    [InlineData(5, false, true)]
    [InlineData(7, true, false)]
    [InlineData(7, false, true)]
    public void Start_WithSpyCharactersThatFit_Starts(int players, bool deepCover, bool blindSpy)
    {
        var game = LobbyOf(players, rules =>
        {
            rules.DeepCoverEnabled = deepCover;
            rules.BlindSpyEnabled = blindSpy;
            rules.FalseCommanderEnabled = !deepCover && !blindSpy;
        });

        Assert.Null(game.Start(Now, new Random(2)).Failure);
    }

    [Fact]
    public void Start_WithAThirdSpyCharacterAtTenPlayers_Starts()
    {
        var game = LobbyOf(10, rules =>
        {
            rules.DeepCoverEnabled = true;
            rules.BlindSpyEnabled = true;
            rules.FalseCommanderEnabled = true;
        });

        Assert.Null(game.Start(Now, new Random(3)).Failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Start_WithABodyGuardAtFivePlayersAndNoCover_IsRefused(bool withBlindSpy)
    {
        var game = LobbyOf(5, rules =>
        {
            rules.BodyGuardEnabled = true;
            rules.BlindSpyEnabled = withBlindSpy;
        });

        var outcome = game.Start(Now, new Random(1));

        Assert.Equal(ResistanceFailure.BodyGuardNeedsCover, outcome.Failure);
        Assert.False(game.IsStarted);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Start_WithABodyGuardAtFivePlayersAndDeepCoverOrAFalseCommander_Starts(bool deepCover, bool falseCommander)
    {
        var game = LobbyOf(5, rules =>
        {
            rules.BodyGuardEnabled = true;
            rules.DeepCoverEnabled = deepCover;
            rules.FalseCommanderEnabled = falseCommander;
        });

        Assert.Null(game.Start(Now, new Random(1)).Failure);
    }

    [Fact]
    public void Start_WithABodyGuardAtSixPlayers_NeedsNoCover()
    {
        var game = LobbyOf(6, rules => rules.BodyGuardEnabled = true);

        Assert.Null(game.Start(Now, new Random(1)).Failure);
    }

    [Fact]
    public void Start_WithTheOptionsButNotTheModule_IgnoresThem()
    {
        var game = LobbyOf(5, rules =>
        {
            rules.AssassinEnabled = false;
            rules.DeepCoverEnabled = true;
            rules.BlindSpyEnabled = true;
            rules.BodyGuardEnabled = true;
        });

        Assert.Null(game.Start(Now, new Random(1)).Failure);
        Assert.All(game.Players, x => Assert.Equal(ResistanceCharacter.Standard, x.Character));
    }

    [Fact]
    public void Start_DealsEachEnabledCharacterToTheRightSide()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var game = LobbyOf(7, rules =>
            {
                rules.BodyGuardEnabled = true;
                rules.DeepCoverEnabled = true;
                rules.BlindSpyEnabled = true;
            });

            game.Start(Now, new Random(seed));

            Assert.Equal(ResistanceRole.Resistance, Assert.Single(game.Players, x => x.Character == ResistanceCharacter.BodyGuard).Role);
            Assert.Equal(ResistanceRole.Spy, Assert.Single(game.Players, x => x.Character == ResistanceCharacter.DeepCover).Role);
            Assert.Equal(ResistanceRole.Spy, Assert.Single(game.Players, x => x.Character == ResistanceCharacter.BlindSpy).Role);
            Assert.Equal(ResistanceRole.Spy, Assert.Single(game.Players, x => x.Character == ResistanceCharacter.Assassin).Role);
            Assert.Equal(ResistanceRole.Resistance, Assert.Single(game.Players, x => x.Character == ResistanceCharacter.Commander).Role);
            Assert.DoesNotContain(game.Players, x => x.Character == ResistanceCharacter.FalseCommander);
            Assert.Equal(3, game.Players.Count(x => x.Role == ResistanceRole.Spy));
        }
    }

    [Fact]
    public void Start_WithTheOptionsOff_DealsTheSameRolesAsWithTheBasicModule()
    {
        var basic = LobbyOf(7, _ => { });
        var withOptions = LobbyOf(7, rules => rules.BodyGuardEnabled = true);
        for (var i = 0; i < 7; i++)
        {
            withOptions.Players[i].PlayerId = basic.Players[i].PlayerId;
        }

        basic.Start(Now, new Random(5));
        withOptions.Start(Now, new Random(5));

        Assert.Equal(basic.Players.Select(x => x.Role), withOptions.Players.Select(x => x.Role));
        Assert.Equal(basic.LeaderPlayerId, withOptions.LeaderPlayerId);
        Assert.Equal(
            basic.Players.Select(x => x.Character == ResistanceCharacter.Standard),
            withOptions.Players.Select(x => x.Character == ResistanceCharacter.Standard || x.Character == ResistanceCharacter.BodyGuard));
    }

    [Fact]
    public void KnowsSpy_WithDeepCoverAndABlindSpy_EveryViewerAndTarget()
    {
        // Seats 0 Assassin, 1 Deep Cover, 2 Blind Spy (spies); 3 Commander, 4 Body Guard, 5 and 6 standard resistance
        var game = Table(
            3,
            ResistanceCharacter.Assassin,
            ResistanceCharacter.DeepCover,
            ResistanceCharacter.BlindSpy,
            ResistanceCharacter.Commander,
            ResistanceCharacter.BodyGuard);
        game.Rules.DeepCoverEnabled = true;
        game.Rules.BlindSpyEnabled = true;
        game.Rules.BodyGuardEnabled = true;

        bool[][] expected =
        [
            [false, true, false, false, false, false, false],
            [true, false, false, false, false, false, false],
            [false, false, false, false, false, false, false],
            [true, false, true, false, false, false, false],
            [false, false, false, false, false, false, false],
            [false, false, false, false, false, false, false],
            [false, false, false, false, false, false, false],
        ];
        for (var viewer = 0; viewer < 7; viewer++)
        {
            for (var target = 0; target < 7; target++)
            {
                Assert.Equal(
                    expected[viewer][target],
                    ResistanceKnowledge.KnowsSpy(game, game.Players[viewer], game.Players[target]));
            }
        }
    }

    [Fact]
    public void KnowsCommander_EveryViewerAndTarget_TheBodyGuardCannotTellTheFalseCommanderApart()
    {
        // Seats 0 Assassin, 1 False Commander, 2 standard spy; 3 Commander, 4 Body Guard, 5 and 6 standard resistance
        var game = Table(
            3,
            ResistanceCharacter.Assassin,
            ResistanceCharacter.FalseCommander,
            ResistanceCharacter.Standard,
            ResistanceCharacter.Commander,
            ResistanceCharacter.BodyGuard);
        game.Rules.FalseCommanderEnabled = true;
        game.Rules.BodyGuardEnabled = true;

        for (var viewer = 0; viewer < 7; viewer++)
        {
            for (var target = 0; target < 7; target++)
            {
                var expected = viewer == 4 && target is 1 or 3;
                Assert.Equal(
                    expected,
                    ResistanceKnowledge.KnowsCommander(game, game.Players[viewer], game.Players[target]));
            }
        }
    }

    [Fact]
    public void KnowsSpy_TheFalseCommanderIsAnOrdinarySpyToTheSpiesAndTheCommander()
    {
        var game = Table(
            3,
            ResistanceCharacter.Assassin,
            ResistanceCharacter.FalseCommander,
            ResistanceCharacter.Standard,
            ResistanceCharacter.Commander);

        Assert.True(ResistanceKnowledge.KnowsSpy(game, game.Players[0], game.Players[1]));
        Assert.True(ResistanceKnowledge.KnowsSpy(game, game.Players[2], game.Players[1]));
        Assert.True(ResistanceKnowledge.KnowsSpy(game, game.Players[3], game.Players[1]));
    }

    [Fact]
    public void Restart_ClearsTheOptionalCharacters()
    {
        var game = LobbyOf(7, rules =>
        {
            rules.BodyGuardEnabled = true;
            rules.DeepCoverEnabled = true;
        });
        game.Start(Now, new Random(4));
        game.Phase = ResistancePhase.Assassination;

        game.Restart();

        Assert.All(game.Players, x => Assert.Equal(ResistanceCharacter.Standard, x.Character));
        Assert.True(game.Rules.BodyGuardEnabled);
    }
}
