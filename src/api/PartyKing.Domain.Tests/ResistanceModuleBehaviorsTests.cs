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
}
