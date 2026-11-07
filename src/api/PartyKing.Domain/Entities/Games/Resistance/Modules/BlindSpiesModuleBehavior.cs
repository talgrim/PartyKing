namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>The Blind Spies variant: the spies do not get to know each other.</summary>
internal sealed class BlindSpiesModuleBehavior : IResistanceModuleBehavior
{
    public ResistanceModule Module => ResistanceModule.BlindSpies;

    public ResistanceFailure LockedFailure => ResistanceFailure.BlindSpiesLocked;

    public bool IsOn(ResistanceRules rules)
    {
        return rules.BlindSpiesEnabled;
    }

    public void Switch(ResistanceRules rules, bool on)
    {
        rules.BlindSpiesEnabled = on;
    }
}
