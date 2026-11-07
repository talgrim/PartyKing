using PartyKing.Domain.Entities.Games.Resistance;
using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Domain.Tests;

/// <summary>The Reverser module of <see cref="ResistanceGame"/>: the result of a mission, the cards and the dealing, with no database.</summary>
public class ResistanceGameReverserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 20, 0, 0, TimeSpan.Zero);

    private const ResistanceMissionCardKind Success = ResistanceMissionCardKind.Success;
    private const ResistanceMissionCardKind Fail = ResistanceMissionCardKind.Fail;
    private const ResistanceMissionCardKind Reverse = ResistanceMissionCardKind.Reverse;

    private static ResistanceGame LobbyGame(int players, bool reverser)
    {
        var rules = ResistanceRules.Default();
        rules.ReverserEnabled = reverser;
        var game = new ResistanceGame { SessionId = Guid.NewGuid(), IsOpen = true, Rules = rules };
        for (var i = 0; i < players; i++)
        {
            game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = i });
        }

        return game;
    }

    /// <summary>
    /// Five players, mission 1 being played by seats 0 (a spy), 2 (a Reverser of the resistance) and 1 (a spy Reverser)
    /// when <paramref name="teamSeats"/> says so.
    /// </summary>
    private static ResistanceGame MissionGame(params int[] teamSeats)
    {
        var rules = ResistanceRules.Default();
        rules.ReverserEnabled = true;
        var game = new ResistanceGame
        {
            SessionId = Guid.NewGuid(),
            IsOpen = true,
            StartedAt = Now,
            Phase = ResistancePhase.PlayingMission,
            Rules = rules,
        };
        for (var i = 0; i < 5; i++)
        {
            game.Players.Add(new ResistancePlayer
            {
                PlayerId = Guid.NewGuid(),
                Order = i,
                Role = i < 2 ? ResistanceRole.Spy : ResistanceRole.Resistance,
                Character = i is 1 or 2 ? ResistanceCharacter.Reverser : ResistanceCharacter.Standard,
                IsOnTeam = teamSeats.Contains(i),
            });
        }

        game.LeaderPlayerId = game.Players[0].PlayerId;

        return game;
    }

    private static ResistanceOutcome Play(ResistanceGame game, int seat, ResistanceMissionCardKind card) =>
        game.PlayMissionCard(game.Players[seat].PlayerId, card);

    [Theory]
    [InlineData(1, new[] { Success, Success }, true)]
    [InlineData(1, new[] { Success, Fail }, false)]
    [InlineData(2, new[] { Success, Fail }, true)]
    [InlineData(2, new[] { Fail, Fail }, false)]
    [InlineData(1, new[] { Success, Success, Reverse }, false)]
    [InlineData(1, new[] { Success, Success, Reverse, Reverse }, true)]
    [InlineData(1, new[] { Success, Success, Fail, Reverse }, true)]
    [InlineData(1, new[] { Success, Fail, Reverse, Reverse }, false)]
    [InlineData(1, new[] { Fail, Reverse, Reverse }, false)]
    [InlineData(2, new[] { Fail, Fail, Reverse }, true)]
    [InlineData(2, new[] { Fail, Fail, Reverse, Reverse }, false)]
    [InlineData(2, new[] { Success, Success, Reverse }, false)]
    public void HasSucceeded_TurnsTheResultAroundForEveryOddReverse(
        int requiredFails, ResistanceMissionCardKind[] cards, bool expected)
    {
        Assert.Equal(expected, ResistanceMissionRules.HasSucceeded(cards, requiredFails));
    }

    [Fact]
    public void HasSucceeded_OnATwoFailMission_OneFailAndOneReverseIsAFailure()
    {
        // The single Fail would not have failed the mission, so the Reverse turns a success into a failure
        Assert.False(ResistanceMissionRules.HasSucceeded([Success, Success, Fail, Reverse], requiredFails: 2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetReverser_BeforeTheGameStarts_SwitchesTheModuleOnANewRulesObject(bool open)
    {
        var rules = ResistanceRules.Default();
        rules.PlotCardsEnabled = true;
        rules.InquisitorEnabled = true;
        rules.BlindSpiesEnabled = true;
        var game = new ResistanceGame { IsOpen = open, Rules = rules };

        var outcome = game.SetReverser(true);

        Assert.Null(outcome.Failure);
        Assert.True(game.Rules.ReverserEnabled);
        Assert.True(game.Rules.PlotCardsEnabled);
        Assert.True(game.Rules.InquisitorEnabled);
        Assert.True(game.Rules.BlindSpiesEnabled);
        Assert.NotSame(rules, game.Rules);
        Assert.False(rules.ReverserEnabled);
    }

    [Fact]
    public void SetReverser_CanSwitchTheModuleBackOff()
    {
        var game = new ResistanceGame { Rules = ResistanceRules.Default() };
        game.SetReverser(true);

        Assert.Null(game.SetReverser(false).Failure);
        Assert.False(game.Rules.ReverserEnabled);
    }

    [Fact]
    public void SetReverser_OnceTheGameStarted_IsLocked()
    {
        var game = new ResistanceGame { StartedAt = Now, Rules = ResistanceRules.Default() };

        var outcome = game.SetReverser(true);

        Assert.Equal(ResistanceFailure.ReverserLocked, outcome.Failure);
        Assert.False(outcome.Changed);
        Assert.False(game.Rules.ReverserEnabled);
    }

    [Fact]
    public void TheOtherSwitchesAndRefreshingTheRules_KeepTheModule()
    {
        var game = new ResistanceGame { Rules = ResistanceRules.Default() };
        game.SetReverser(true);

        game.SetPlotCards(true);
        game.SetInquisitor(true);
        game.SetBlindSpies(true);
        game.RefreshRules(ResistanceRules.Default());

        Assert.True(game.Rules.ReverserEnabled);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void Start_WithTheModule_DealsOneReverserToEachSide(int players)
    {
        for (var seed = 0; seed < 25; seed++)
        {
            var game = LobbyGame(players, reverser: true);

            game.Start(Now, new Random(seed));

            Assert.Equal(2, game.Players.Count(x => x.Character == ResistanceCharacter.Reverser));
            Assert.Single(game.Players, x => x.Character == ResistanceCharacter.Reverser && x.Role == ResistanceRole.Spy);
            Assert.Single(game.Players, x => x.Character == ResistanceCharacter.Reverser && x.Role == ResistanceRole.Resistance);
        }
    }

    [Theory]
    [InlineData(ResistanceReverserSides.Resistance, 0, 1)]
    [InlineData(ResistanceReverserSides.Spy, 1, 0)]
    [InlineData(ResistanceReverserSides.Both, 1, 1)]
    public void Start_DealsAReverserOnlyToTheChosenSides(ResistanceReverserSides sides, int spies, int resistance)
    {
        for (var seed = 0; seed < 25; seed++)
        {
            var game = LobbyGame(6, reverser: true);
            game.Rules.ReverserSides = sides;

            game.Start(Now, new Random(seed));

            Assert.Equal(spies, game.Players.Count(x => x.Character == ResistanceCharacter.Reverser && x.Role == ResistanceRole.Spy));
            Assert.Equal(
                resistance,
                game.Players.Count(x => x.Character == ResistanceCharacter.Reverser && x.Role == ResistanceRole.Resistance));
        }
    }

    [Fact]
    public void SetReverser_ChoosesTheSides_AndKeepsThemWhenNoneIsGiven()
    {
        var game = new ResistanceGame { Rules = ResistanceRules.Default() };
        Assert.Equal(ResistanceReverserSides.Both, game.Rules.ReverserSides);

        Assert.Null(game.SetReverser(true, ResistanceReverserSides.Spy).Failure);
        Assert.Equal(ResistanceReverserSides.Spy, game.Rules.ReverserSides);

        game.SetReverser(false);
        Assert.Equal(ResistanceReverserSides.Spy, game.Rules.ReverserSides);

        game.SetPlotCards(true);
        game.RefreshRules(ResistanceRules.Default());
        Assert.Equal(ResistanceReverserSides.Spy, game.Rules.ReverserSides);
    }

    [Fact]
    public void SetReverser_OnceTheGameStarted_CannotChangeTheSidesEither()
    {
        var game = new ResistanceGame { StartedAt = Now, Rules = ResistanceRules.Default() };

        Assert.Equal(ResistanceFailure.ReverserLocked, game.SetReverser(true, ResistanceReverserSides.Spy).Failure);
        Assert.Equal(ResistanceReverserSides.Both, game.Rules.ReverserSides);
    }

    [Fact]
    public void Start_WithoutTheModule_DealsNoCharacters()
    {
        var game = LobbyGame(7, reverser: false);

        game.Start(Now, new Random(3));

        Assert.All(game.Players, x => Assert.Equal(ResistanceCharacter.Standard, x.Character));
    }

    [Fact]
    public void Restart_ClearsTheCharactersAndTheDealIsNewEachTime()
    {
        var game = LobbyGame(7, reverser: true);
        game.Start(Now, new Random(1));
        game.Phase = ResistancePhase.GameOver;

        game.Restart();

        Assert.All(game.Players, x => Assert.Equal(ResistanceCharacter.Standard, x.Character));
        game.Start(Now, new Random(2));
        Assert.Equal(2, game.Players.Count(x => x.Character == ResistanceCharacter.Reverser));
    }

    [Fact]
    public void AReverser_OfTheResistance_MayPlayReverseOrSuccessButNotFail()
    {
        var game = MissionGame(0, 2);

        Assert.Equal(ResistanceFailure.MustSucceed, Play(game, 2, Fail).Failure);
        Assert.Null(game.Players[2].MissionCard);
        Assert.Null(Play(game, 2, Reverse).Failure);
        Assert.Equal(Reverse, game.Players[2].MissionCard);
    }

    [Fact]
    public void ASpyReverser_MayPlayReverseOrSuccessButNeverFail()
    {
        var game = MissionGame(0, 1);

        Assert.Equal(ResistanceFailure.ReverserCannotFail, Play(game, 1, Fail).Failure);
        Assert.Null(game.Players[1].MissionCard);
        Assert.Null(Play(game, 1, Success).Failure);
        Assert.Equal(Success, game.Players[1].MissionCard);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void ANonReverser_CannotPlayReverse(int seat)
    {
        var game = MissionGame(seat, 4);

        var outcome = Play(game, seat, Reverse);

        Assert.Equal(ResistanceFailure.NotReverser, outcome.Failure);
        Assert.False(outcome.Changed);
        Assert.Null(game.Players[seat].MissionCard);
    }

    [Fact]
    public void ReverseCards_AreNotPlayableWithoutTheModule()
    {
        var game = MissionGame(0, 2);
        foreach (var player in game.Players)
        {
            player.Character = ResistanceCharacter.Standard;
        }

        Assert.Equal(ResistanceFailure.NotReverser, Play(game, 2, Reverse).Failure);
    }

    [Fact]
    public void AReverseCard_TurnsAFailedMissionIntoASuccess_AndTheCardsPlayedArePublic()
    {
        var game = MissionGame(0, 2, 3);
        Play(game, 0, Fail);
        Play(game, 2, Reverse);
        Play(game, 3, Success);

        var result = Assert.Single(game.MissionResults);
        Assert.True(result.Succeeded);
        Assert.Equal((1, 1), (result.FailCount, result.ReverseCount));
        var completed = game.DrainEvents().OfType<ResistanceMissionCompleted>().Single();
        Assert.Equal(new ResistanceMissionCompleted(game.SessionId, 1, true, 1, 1), completed);
    }

    [Fact]
    public void AReverseCard_TurnsASuccessfulMissionIntoAFailure()
    {
        var game = MissionGame(2, 3);
        Play(game, 2, Reverse);
        Play(game, 3, Success);

        Assert.False(Assert.Single(game.MissionResults).Succeeded);
        Assert.Equal(1, game.SpyScore);
    }

    [Fact]
    public void TwoReverseCards_CancelOut()
    {
        var game = MissionGame(0, 1, 2);
        Play(game, 0, Fail);
        Play(game, 1, Reverse);
        Play(game, 2, Reverse);

        Assert.False(Assert.Single(game.MissionResults).Succeeded);
    }

    [Fact]
    public void WithoutReverseCards_TheCompletedMissionCountsNoReverse()
    {
        var game = MissionGame(0, 3);
        game.Rules.ReverserEnabled = false;
        Play(game, 0, Fail);
        Play(game, 3, Success);

        var completed = game.DrainEvents().OfType<ResistanceMissionCompleted>().Single();
        Assert.Equal((1, 0), (completed.FailCount, completed.ReverseCount));
    }

    [Fact]
    public void ASpyReverser_KnowsTheSpiesByDefault()
    {
        var rules = ResistanceRules.Default();

        Assert.True(rules.ReverserSpyKnowsSpies);
        Assert.True(rules.Clone().ReverserSpyKnowsSpies);

        rules.ReverserSpyKnowsSpies = false;
        Assert.False(rules.Clone().ReverserSpyKnowsSpies);
    }

    [Fact]
    public void SetReverser_ChoosesWhetherTheSpyReverserKnowsTheSpies_AndKeepsItWhenNoneIsGiven()
    {
        var game = new ResistanceGame { Rules = ResistanceRules.Default() };

        Assert.Null(game.SetReverser(true, spyKnowsSpies: false).Failure);
        Assert.False(game.Rules.ReverserSpyKnowsSpies);

        game.SetReverser(false);
        Assert.False(game.Rules.ReverserSpyKnowsSpies);

        game.SetPlotCards(true);
        game.RefreshRules(ResistanceRules.Default());
        Assert.False(game.Rules.ReverserSpyKnowsSpies);

        game.SetReverser(true, spyKnowsSpies: true);
        Assert.True(game.Rules.ReverserSpyKnowsSpies);
    }

    [Fact]
    public void SetReverser_OnceTheGameStarted_CannotChangeWhetherTheSpyReverserKnowsTheSpies()
    {
        var game = new ResistanceGame { StartedAt = Now, Rules = ResistanceRules.Default() };

        Assert.Equal(ResistanceFailure.ReverserLocked, game.SetReverser(true, spyKnowsSpies: false).Failure);
        Assert.True(game.Rules.ReverserSpyKnowsSpies);
    }

    /// <summary>
    /// The whole viewer x target table of <see cref="MissionGame"/>: seat 0 a spy, seat 1 a spy Reverser, seat 2 a
    /// resistance Reverser. The Reverser spy is only left out of the reveal when the rules say so; a resistance Reverser
    /// is never a spy and the switch changes nothing about them.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void KnowsSpy_EveryViewerAndTarget_WithAReverserSpy(bool reverser, bool spyKnowsSpies)
    {
        var game = MissionGame();
        game.Rules.ReverserEnabled = reverser;
        game.Rules.ReverserSpyKnowsSpies = spyKnowsSpies;
        if (!reverser)
        {
            // Without the module nobody holds the character
            foreach (var player in game.Players)
            {
                player.Character = ResistanceCharacter.Standard;
            }
        }

        foreach (var viewer in game.Players)
        {
            foreach (var target in game.Players)
            {
                var hidden = reverser && !spyKnowsSpies && (viewer.Order == 1 || target.Order == 1);
                var expected = !hidden
                               && viewer.Role == ResistanceRole.Spy
                               && target.Role == ResistanceRole.Spy
                               && viewer.PlayerId != target.PlayerId;

                Assert.Equal(expected, ResistanceKnowledge.KnowsSpy(game, viewer, target));
            }
        }
    }

    [Fact]
    public void Loyalty_IsTheAllegiance_WhateverTheCharacter()
    {
        var game = MissionGame();

        Assert.Equal(ResistanceRole.Spy, ResistanceKnowledge.Loyalty(game.Players[1]));
        Assert.Equal(ResistanceRole.Resistance, ResistanceKnowledge.Loyalty(game.Players[2]));
        Assert.Equal(ResistanceRole.Resistance, ResistanceKnowledge.Loyalty(game.Players[3]));
        Assert.Null(ResistanceKnowledge.Loyalty(new ResistancePlayer()));
    }
}
