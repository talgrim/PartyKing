using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Domain.Tests;

/// <summary>The Defector module of <see cref="ResistanceGame"/>: the dealing, the switch deck, who knows whom, with no database.</summary>
public class ResistanceGameDefectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 20, 0, 0, TimeSpan.Zero);

    private static ResistanceGame StartedGame(
        int players, int seed, bool defector = true, bool reverser = false, bool knowEachOther = false)
    {
        var rules = ResistanceRules.Default();
        rules.DefectorEnabled = defector;
        rules.DefectorsKnowEachOther = knowEachOther;
        rules.ReverserEnabled = reverser;
        var game = new ResistanceGame { SessionId = Guid.NewGuid(), IsOpen = true, Rules = rules };
        for (var i = 0; i < players; i++)
        {
            game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = i });
        }

        Assert.Null(game.Start(Now, new Random(seed)).Failure);

        return game;
    }

    private static List<ResistancePlayer> Defectors(ResistanceGame game) =>
        game.Players.Where(x => x.Character == ResistanceCharacter.Defector).ToList();

    private static void NextMission(ResistanceGame game) => ResistanceFlow.PassLeadership(game);

    [Fact]
    public void TheDefectorsAreOffByDefault_AndTheFlagsSurviveClone()
    {
        var rules = ResistanceRules.Default();

        Assert.False(rules.DefectorEnabled);
        Assert.False(rules.DefectorsKnowEachOther);

        rules.DefectorEnabled = true;
        var copy = rules.Clone();
        Assert.True(copy.DefectorEnabled);
        Assert.False(copy.DefectorsKnowEachOther);

        rules.DefectorsKnowEachOther = true;
        Assert.True(rules.Clone().Clone().DefectorsKnowEachOther);
    }

    [Fact]
    public void SetDefector_SwitchesTheModuleOnANewRulesObject_AndKeepsTheChoiceWhenNoneIsGiven()
    {
        var rules = ResistanceRules.Default();
        rules.ReverserEnabled = true;
        var game = new ResistanceGame { Rules = rules };

        Assert.Null(game.SetDefector(true, defectorsKnowEachOther: true).Failure);
        Assert.True(game.Rules.DefectorEnabled);
        Assert.True(game.Rules.DefectorsKnowEachOther);
        Assert.True(game.Rules.ReverserEnabled);
        Assert.NotSame(rules, game.Rules);
        Assert.False(rules.DefectorEnabled);

        game.SetDefector(false);
        Assert.False(game.Rules.DefectorEnabled);
        Assert.True(game.Rules.DefectorsKnowEachOther);

        // A refresh from the server configuration keeps the module and the choice
        game.SetDefector(true);
        game.RefreshRules(ResistanceRules.Default());
        Assert.True(game.Rules.DefectorEnabled);
        Assert.True(game.Rules.DefectorsKnowEachOther);
    }

    [Fact]
    public void SetDefector_OnceTheGameStarted_IsRefused()
    {
        var game = new ResistanceGame { StartedAt = Now, Rules = ResistanceRules.Default() };

        Assert.Equal(ResistanceFailure.DefectorLocked, game.SetDefector(true).Failure);
        Assert.False(game.Rules.DefectorEnabled);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(10)]
    public void Start_DealsOneDefectorToEachSide_AndAShuffledDeckOfThreeNoChangeAndTwoChange(int players)
    {
        for (var seed = 0; seed < 40; seed++)
        {
            var game = StartedGame(players, seed);

            var defectors = Defectors(game);
            Assert.Equal(2, defectors.Count);
            Assert.Equal(
                [ResistanceRole.Resistance, ResistanceRole.Spy],
                defectors.Select(x => x.Role!.Value).Order().ToArray());
            Assert.Equal(5, game.DefectorDeck.Cards.Length);
            Assert.Equal(3, game.DefectorDeck.Cards.Count(x => x == 'N'));
            Assert.Equal(2, game.DefectorDeck.Cards.Count(x => x == 'C'));
            Assert.Empty(game.DefectorFlips);
        }
    }

    [Fact]
    public void Start_ShufflesTheDeck()
    {
        var decks = Enumerable.Range(0, 40).Select(seed => StartedGame(5, seed).DefectorDeck.Cards).Distinct().ToList();

        Assert.True(decks.Count > 3);
    }

    [Fact]
    public void Start_WithoutTheModule_DealsNoDefectorAndNoDeck()
    {
        var game = StartedGame(6, 1, defector: false);

        Assert.Empty(Defectors(game));
        Assert.Empty(game.DefectorDeck.Cards);
        Assert.Empty(game.DefectorFlips);
    }

    [Fact]
    public void Start_WithTheReverser_NeverGivesOnePlayerBothCharacters()
    {
        for (var seed = 0; seed < 60; seed++)
        {
            var game = StartedGame(5, seed, reverser: true);

            Assert.Equal(2, game.Players.Count(x => x.Character == ResistanceCharacter.Defector));
            Assert.Equal(2, game.Players.Count(x => x.Character == ResistanceCharacter.Reverser));
            Assert.Equal(1, game.Players.Count(x => x.Character == ResistanceCharacter.Standard));
            Assert.Single(game.Players, x => x.Character == ResistanceCharacter.Defector && x.Role == ResistanceRole.Spy);
        }
    }

    [Fact]
    public void TheFirstTwoMissions_FlipNothing_AndTheThirdTheFourthAndTheFifthFlipOneCardEach()
    {
        var game = StartedGame(5, 3);
        game.DefectorDeck.Cards = "NNNCC";

        NextMission(game);
        Assert.Equal(2, game.MissionNumber);
        Assert.Empty(game.DefectorFlips);

        NextMission(game);
        Assert.Equal([false], game.DefectorFlips);

        NextMission(game);
        Assert.Equal([false, false], game.DefectorFlips);

        NextMission(game);
        Assert.Equal([false, false, false], game.DefectorFlips);
        Assert.Equal(5, game.MissionNumber);
    }

    [Fact]
    public void ChangeAllegiance_PutsBothDefectorsOnTheOtherSide_AndTheNextOneBack()
    {
        var game = StartedGame(5, 3);
        game.DefectorDeck.Cards = "CCNNN";
        var spyDefector = Defectors(game).Single(x => x.Role == ResistanceRole.Spy);
        var resistanceDefector = Defectors(game).Single(x => x.Role == ResistanceRole.Resistance);
        var others = game.Players.Where(x => x.Character == ResistanceCharacter.Standard).Select(x => (x, x.Role)).ToList();

        NextMission(game);
        NextMission(game);
        Assert.Equal([true], game.DefectorFlips);
        Assert.Equal(ResistanceRole.Resistance, spyDefector.Role);
        Assert.Equal(ResistanceRole.Spy, resistanceDefector.Role);
        Assert.Equal(ResistanceCharacter.Defector, spyDefector.Character);

        NextMission(game);
        Assert.Equal([true, true], game.DefectorFlips);
        Assert.Equal(ResistanceRole.Spy, spyDefector.Role);
        Assert.Equal(ResistanceRole.Resistance, resistanceDefector.Role);
        Assert.All(others, x => Assert.Equal(x.Role, x.x.Role));
    }

    [Fact]
    public void ARejectedTeam_FlipsNoCard()
    {
        var game = StartedGame(5, 3);
        game.DefectorDeck.Cards = "NNCCN";
        NextMission(game);
        NextMission(game);
        Assert.Equal([false], game.DefectorFlips);
        var roles = game.Players.Select(x => x.Role).ToList();

        var team = game.Players.Take(game.Rules.TeamSize(5, game.MissionNumber)).Select(x => x.PlayerId).ToList();
        Assert.Null(game.SelectTeam(game.LeaderPlayerId, team).Failure);
        foreach (var player in game.Players)
        {
            Assert.Null(game.Vote(player.PlayerId, false).Failure);
        }

        Assert.Equal(1, game.RejectedTeams);
        Assert.Equal(3, game.MissionNumber);
        Assert.Equal([false], game.DefectorFlips);
        Assert.Equal(roles, game.Players.Select(x => x.Role));
    }

    [Fact]
    public void AfterASwitch_TheDefectorsPlayByTheirNewSide()
    {
        var game = StartedGame(5, 3);
        game.DefectorDeck.Cards = "CNNNN";
        var formerSpy = Defectors(game).Single(x => x.Role == ResistanceRole.Spy);
        var formerResistance = Defectors(game).Single(x => x.Role == ResistanceRole.Resistance);
        NextMission(game);
        NextMission(game);
        game.Phase = ResistancePhase.PlayingMission;
        foreach (var player in game.Players)
        {
            player.IsOnTeam = player == formerSpy || player == formerResistance;
        }

        Assert.Equal(
            ResistanceFailure.MustSucceed,
            game.PlayMissionCard(formerSpy.PlayerId, ResistanceMissionCardKind.Fail).Failure);
        Assert.Null(game.PlayMissionCard(formerResistance.PlayerId, ResistanceMissionCardKind.Fail).Failure);
    }

    [Fact]
    public void Restart_ClearsTheDeck_AndTheNextStartDealsANewOne()
    {
        var game = StartedGame(5, 3);
        game.DefectorDeck.Cards = "NNNCC";
        game.Phase = ResistancePhase.GameOver;

        Assert.Null(game.Restart().Failure);

        Assert.Empty(game.DefectorDeck.Cards);
        Assert.Empty(game.DefectorFlips);
        Assert.All(game.Players, x => Assert.Equal(ResistanceCharacter.Standard, x.Character));

        game.IsOpen = true;
        Assert.Null(game.Start(Now, new Random(9)).Failure);
        Assert.Equal(5, game.DefectorDeck.Cards.Length);
        Assert.Equal(2, Defectors(game).Count);
    }

    /// <summary>
    /// The whole viewer x target table of <see cref="ResistanceKnowledge.KnowsSpy"/> before the first switch and after
    /// one and two: the Defector who started as a spy is known to the spies while they are spies, the other Defector is
    /// never known to them, and no Defector knows any spy.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void KnowsSpy_EveryViewerAndTarget_BeforeAndAfterTheSwitches(int switches)
    {
        var game = StartedGame(7, 4);
        game.DefectorDeck.Cards = "CCNNN";
        var startedAsSpy = Defectors(game).Single(x => x.Role == ResistanceRole.Spy);
        var startedAsResistance = Defectors(game).Single(x => x.Role == ResistanceRole.Resistance);
        var standardSpies = game.Players
            .Where(x => x.Character == ResistanceCharacter.Standard && x.Role == ResistanceRole.Spy)
            .ToList();
        Assert.Equal(2, standardSpies.Count);
        for (var i = 0; i < 1 + switches; i++)
        {
            NextMission(game);
        }

        Assert.Equal(switches, game.DefectorFlips.Count(x => x));

        foreach (var viewer in game.Players)
        {
            foreach (var target in game.Players)
            {
                var spyNow = viewer.Role == ResistanceRole.Spy && target.Role == ResistanceRole.Spy;
                var expected = spyNow
                               && viewer.PlayerId != target.PlayerId
                               && viewer != startedAsSpy
                               && viewer != startedAsResistance
                               && target != startedAsResistance;

                Assert.Equal(expected, ResistanceKnowledge.KnowsSpy(game, viewer, target));
            }
        }

        // The spies know the Defector who started among them exactly while they are a spy
        Assert.Equal(
            switches != 1,
            ResistanceKnowledge.KnowsSpy(game, standardSpies[0], startedAsSpy));
        Assert.False(ResistanceKnowledge.KnowsSpy(game, standardSpies[0], startedAsResistance));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void KnowsDefector_OnlyBetweenTheTwoDefectors_AndOnlyWithTheVariant(bool variant, bool expectedBetweenDefectors)
    {
        var game = StartedGame(6, 2, knowEachOther: variant);
        var defectors = Defectors(game);

        foreach (var viewer in game.Players)
        {
            foreach (var target in game.Players)
            {
                var expected = expectedBetweenDefectors
                               && defectors.Contains(viewer)
                               && defectors.Contains(target)
                               && viewer != target;

                Assert.Equal(expected, ResistanceKnowledge.KnowsDefector(game, viewer, target));
            }
        }
    }

    [Fact]
    public void KnowsDefector_WithTheModuleOff_IsAlwaysFalse()
    {
        var game = StartedGame(5, 2, defector: false);
        game.Rules.DefectorsKnowEachOther = true;

        Assert.All(game.Players, viewer =>
            Assert.All(game.Players, target => Assert.False(ResistanceKnowledge.KnowsDefector(game, viewer, target))));
    }

    [Fact]
    public void KnowsDefector_StaysTrueAfterASwitch()
    {
        var game = StartedGame(5, 2, knowEachOther: true);
        game.DefectorDeck.Cards = "CNNNN";
        NextMission(game);
        NextMission(game);
        var defectors = Defectors(game);

        Assert.True(ResistanceKnowledge.KnowsDefector(game, defectors[0], defectors[1]));
        Assert.True(ResistanceKnowledge.KnowsDefector(game, defectors[1], defectors[0]));
    }
}
