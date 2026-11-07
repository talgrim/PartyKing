namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>The Inquisitor module (Hostile Intent).</summary>
internal sealed class InquisitorModuleBehavior : IResistanceModuleBehavior
{
    public ResistanceModule Module => ResistanceModule.Inquisitor;

    public ResistanceFailure LockedFailure => ResistanceFailure.InquisitorLocked;

    public bool IsOn(ResistanceRules rules)
    {
        return rules.InquisitorEnabled;
    }

    public void Switch(ResistanceRules rules, bool on)
    {
        rules.InquisitorEnabled = on;
    }
}
