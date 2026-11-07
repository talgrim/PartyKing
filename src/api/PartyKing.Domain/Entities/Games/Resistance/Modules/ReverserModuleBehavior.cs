namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>The Reverser module.</summary>
internal sealed class ReverserModuleBehavior : IResistanceModuleBehavior
{
    public ResistanceModule Module => ResistanceModule.Reverser;

    public ResistanceFailure LockedFailure => ResistanceFailure.ReverserLocked;

    public bool IsOn(ResistanceRules rules)
    {
        return rules.ReverserEnabled;
    }

    public void Switch(ResistanceRules rules, bool on)
    {
        rules.ReverserEnabled = on;
    }
}
