using PartyKing.Domain.Entities.Games.Resistance;
using PartyKing.Domain.Entities.Games.Resistance.Modules;

namespace PartyKing.Domain.Tests;

/// <summary>The registry of the rules modules (<see cref="ResistanceModules"/>) and what the module behaviors do on their own.</summary>
public class ResistanceModuleBehaviorsTests
{
    /// <summary>A module that only reads one flag of the rules and declares the conflicts it is given.</summary>
    private sealed class FakeModule(
        ResistanceModule module,
        Func<ResistanceRules, bool> isOn,
        params ResistanceModuleConflict[] conflicts) : IResistanceModuleBehavior
    {
        public ResistanceModule Module => module;

        public ResistanceFailure LockedFailure => ResistanceFailure.PlotCardsLocked;

        public IReadOnlyList<ResistanceModuleConflict> Conflicts { get; } = conflicts;

        public bool IsOn(ResistanceRules rules) => isOn(rules);

        public void Switch(ResistanceRules rules, bool on)
        {
        }
    }

    /// <summary>A module that only fails the start with the failure it is given.</summary>
    private sealed class FailingStart(ResistanceModule module, ResistanceFailure? failure) : IDealBehavior
    {
        public ResistanceModule Module => module;

        public ResistanceFailure LockedFailure => ResistanceFailure.PlotCardsLocked;

        public bool IsOn(ResistanceRules rules) => true;

        public void Switch(ResistanceRules rules, bool on)
        {
        }

        public ResistanceFailure? StartFailure(ResistanceGame game) => failure;
    }

    [Fact]
    public void TheRegistry_HasOneBehaviorPerModule_InTheOrderOfTheEnum()
    {
        var registered = ResistanceModules.Registered.Select(x => x.Module).ToList();

        Assert.Equal(Enum.GetValues<ResistanceModule>(), registered);
    }

    [Fact]
    public void AModuleWithoutABehavior_IsAnErrorAndNotAPlainModule()
    {
        Assert.Throws<InvalidOperationException>(() => ResistanceModules.For((ResistanceModule)999));
    }

    [Fact]
    public void WhenMoreThanOneConflictApplies_TheFirstModuleInTheGivenOrderAnswers()
    {
        // Reverser is switched on while Inquisitor and Blind Spies are on, and both of them refuse it, each in their own words
        var inquisitor = new FakeModule(
            ResistanceModule.Inquisitor,
            x => x.InquisitorEnabled,
            new ResistanceModuleConflict(ResistanceModule.Reverser, ResistanceFailure.InquisitorLocked));
        var blindSpies = new FakeModule(
            ResistanceModule.BlindSpies,
            x => x.BlindSpiesEnabled,
            new ResistanceModuleConflict(ResistanceModule.Reverser, ResistanceFailure.BlindSpiesLocked));
        var reverser = new FakeModule(ResistanceModule.Reverser, x => x.ReverserEnabled);
        var rules = ResistanceRules.Default();
        rules.InquisitorEnabled = true;
        rules.BlindSpiesEnabled = true;

        Assert.Equal(
            ResistanceFailure.InquisitorLocked,
            ResistanceModules.ConflictFailure([inquisitor, blindSpies, reverser], rules, ResistanceModule.Reverser));
        Assert.Equal(
            ResistanceFailure.BlindSpiesLocked,
            ResistanceModules.ConflictFailure([blindSpies, inquisitor, reverser], rules, ResistanceModule.Reverser));
    }

    [Fact]
    public void WhenAModuleDeclaresMoreThanOneConflict_TheFirstItDeclaredAnswers()
    {
        var assassin = new FakeModule(
            ResistanceModule.Assassin,
            x => x.AssassinEnabled,
            new ResistanceModuleConflict(ResistanceModule.Defector, ResistanceFailure.DefectorLocked),
            new ResistanceModuleConflict(ResistanceModule.Reverser, ResistanceFailure.ReverserLocked));
        var defector = new FakeModule(ResistanceModule.Defector, x => x.DefectorEnabled);
        var reverser = new FakeModule(ResistanceModule.Reverser, x => x.ReverserEnabled);
        var rules = ResistanceRules.Default();
        rules.DefectorEnabled = true;
        rules.ReverserEnabled = true;

        Assert.Equal(
            ResistanceFailure.DefectorLocked,
            ResistanceModules.ConflictFailure([assassin, defector, reverser], rules, ResistanceModule.Assassin));
    }

    [Fact]
    public void ThePlayedModules_ConflictWithTheAssassinWhicheverSideIsAskedFrom()
    {
        var rules = ResistanceRules.Default();
        rules.AssassinEnabled = true;

        foreach (var other in new[] { ResistanceModule.Defector, ResistanceModule.Reverser, ResistanceModule.BlindSpies })
        {
            Assert.Equal(ResistanceFailure.AssassinConflict, ResistanceModules.ConflictFailure(rules, other));
        }

        Assert.Null(ResistanceModules.ConflictFailure(rules, ResistanceModule.Inquisitor));
        Assert.Null(ResistanceModules.ConflictFailure(rules, ResistanceModule.PlotCards));
    }

    [Fact]
    public void SwitchingTheAssassinOffThroughTheRegistry_SwitchesItsOptionalCharactersOffToo()
    {
        var rules = ResistanceRules.Default();
        ResistanceModules.Switch(rules, ResistanceModule.Assassin, true);
        rules.BodyGuardEnabled = true;
        rules.DeepCoverEnabled = true;
        rules.BlindSpyEnabled = true;
        rules.FalseCommanderEnabled = true;

        ResistanceModules.Switch(rules, ResistanceModule.Assassin, false);

        Assert.False(rules.AssassinEnabled);
        Assert.False(rules.BodyGuardEnabled);
        Assert.False(rules.DeepCoverEnabled);
        Assert.False(rules.BlindSpyEnabled);
        Assert.False(rules.FalseCommanderEnabled);
    }

    [Fact]
    public void SwitchingTheAssassinOnThroughTheRegistry_KeepsTheChoicesOfItsOptionalCharacters()
    {
        var rules = ResistanceRules.Default();
        rules.BodyGuardEnabled = true;
        rules.FalseCommanderEnabled = true;

        ResistanceModules.Switch(rules, ResistanceModule.Assassin, true);

        Assert.True(rules.AssassinEnabled);
        Assert.True(rules.BodyGuardEnabled);
        Assert.True(rules.FalseCommanderEnabled);
        Assert.False(rules.DeepCoverEnabled);
    }

    [Fact]
    public void TheModulesThatDealACardOrAToken_AreAskedInTheOrderOfTheRegistry()
    {
        Assert.Equal(
            [
                ResistanceModule.PlotCards,
                ResistanceModule.Inquisitor,
                ResistanceModule.Defector,
                ResistanceModule.Assassin,
            ],
            ResistanceModules.Dealing.Select(x => x.Module));
    }

    [Fact]
    public void TheModulesThatDealCharacters_TakePlayersInTheOrderOfTheRegistry()
    {
        Assert.Equal(
            [ResistanceModule.Reverser, ResistanceModule.Defector, ResistanceModule.Assassin],
            ResistanceModules.CharacterDealers.Select(x => x.Module));
    }

    [Fact]
    public void TheStartFailure_IsTheFirstOneInTheOrderOfTheModules()
    {
        var game = new ResistanceGame { Rules = ResistanceRules.Default() };
        var none = new FailingStart(ResistanceModule.PlotCards, null);
        var first = new FailingStart(ResistanceModule.Inquisitor, ResistanceFailure.TooManySpyCharacters);
        var second = new FailingStart(ResistanceModule.Reverser, ResistanceFailure.BodyGuardNeedsCover);

        Assert.Equal(ResistanceFailure.TooManySpyCharacters, ResistanceModules.StartFailure([none, first, second], game));
        Assert.Equal(ResistanceFailure.BodyGuardNeedsCover, ResistanceModules.StartFailure([none, second, first], game));
        Assert.Null(ResistanceModules.StartFailure([none], game));
    }

    [Fact]
    public void Reset_ClearsWhatTheModulesAccumulated_WhetherTheyAreOnOrNot()
    {
        var game = new ResistanceGame { SessionId = Guid.NewGuid(), Rules = ResistanceRules.Default() };
        game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = 0, WasInquisitor = true });
        game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = 1 });
        game.InquisitorPlayerId = game.Players[0].PlayerId;
        game.InquisitionExaminerPlayerId = game.Players[1].PlayerId;
        game.DefectorDeck = "CNNNN";
        game.AssassinationTargetPlayerId = game.Players[1].PlayerId;
        game.PlotCards.Add(new ResistancePlotCard { CardId = 0, SessionId = game.SessionId });

        ResistanceDealer.Reset(game);

        Assert.All(game.Players, x => Assert.False(x.WasInquisitor));
        Assert.Null(game.InquisitorPlayerId);
        Assert.Null(game.InquisitionExaminerPlayerId);
        Assert.Equal(string.Empty, game.DefectorDeck);
        Assert.Null(game.AssassinationTargetPlayerId);
        Assert.Empty(game.PlotCards);
    }

    private static ResistanceGame StartedGame(Action<ResistanceRules> configure, int players = 7)
    {
        var rules = ResistanceRules.Default();
        configure(rules);
        var game = new ResistanceGame { SessionId = Guid.NewGuid(), IsOpen = true, Rules = rules };
        for (var i = 0; i < players; i++)
        {
            game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = i });
        }

        Assert.Null(game.Start(new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero), new Random(4)).Failure);

        return game;
    }

    /// <summary>
    /// A module that deals a character does it through <see cref="ICharacterDealBehavior"/>, which is also what lets its
    /// players be told their own character: a module that deals one some other way would leave them a blank card.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryModule))]
    public void AModuleThatDealsACharacter_SaysSo_AndItsPlayersAreToldTheirOwn(string moduleName)
    {
        var module = Enum.Parse<ResistanceModule>(moduleName);
        var game = StartedGame(x => ResistanceModules.Switch(x, module, true));
        var dealsACharacter = game.Players.Any(x => x.Character != ResistanceCharacter.Standard);

        Assert.Equal(dealsACharacter, ResistanceModules.For(module) is ICharacterDealBehavior);
        foreach (var player in game.Players)
        {
            Assert.Equal(dealsACharacter ? player.Character : null, ResistanceKnowledge.OwnCharacter(game, player));
        }
    }

    public static TheoryData<string> EveryModule()
    {
        var modules = new TheoryData<string>();
        foreach (var name in Enum.GetNames<ResistanceModule>())
        {
            modules.Add(name);
        }

        return modules;
    }

    [Fact]
    public void OwnCharacter_IsNullForAnObserver_AndBeforeTheStart()
    {
        var started = StartedGame(x => x.AssassinEnabled = true);
        var lobby = new ResistanceGame { Rules = ResistanceRules.Default() };
        lobby.Rules.AssassinEnabled = true;
        lobby.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = 0 });

        Assert.Null(ResistanceKnowledge.OwnCharacter(started, null));
        Assert.Null(ResistanceKnowledge.OwnCharacter(lobby, lobby.Players[0]));
    }

    [Fact]
    public void TheModulesThatRestrictTheMissionCards_AreAskedInTheOrderOfTheRegistry()
    {
        Assert.Equal([ResistanceModule.Reverser], ResistanceModules.MissionCards.Select(x => x.Module));
    }

    [Fact]
    public void ARestartedGame_DealtWithTheModulesOff_KeepsNothingOfTheFirstDeal()
    {
        var game = StartedGame(x =>
        {
            x.DefectorEnabled = true;
            x.PlotCardsEnabled = true;
        });
        Assert.NotEmpty(game.DefectorDeck);
        Assert.NotEmpty(game.PlotCards);
        game.Phase = ResistancePhase.GameOver;

        Assert.Null(game.Restart().Failure);
        Assert.Null(game.SetDefector(false).Failure);
        Assert.Null(game.SetPlotCards(false).Failure);
        Assert.Null(game.Start(new DateTimeOffset(2026, 11, 7, 13, 0, 0, TimeSpan.Zero), new Random(5)).Failure);

        Assert.Equal(string.Empty, game.DefectorDeck);
        Assert.Empty(game.PlotCards);
        Assert.All(game.Players, x => Assert.Equal(ResistanceCharacter.Standard, x.Character));
    }
}
