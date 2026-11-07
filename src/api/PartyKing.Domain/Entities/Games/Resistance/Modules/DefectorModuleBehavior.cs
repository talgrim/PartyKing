namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>The Defector module (Hidden Agenda).</summary>
internal sealed class DefectorModuleBehavior : IResistanceModuleBehavior
{
    public ResistanceModule Module => ResistanceModule.Defector;

    public ResistanceFailure LockedFailure => ResistanceFailure.DefectorLocked;

    public bool IsOn(ResistanceRules rules)
    {
        return rules.DefectorEnabled;
    }

    public void Switch(ResistanceRules rules, bool on)
    {
        rules.DefectorEnabled = on;
    }
}
