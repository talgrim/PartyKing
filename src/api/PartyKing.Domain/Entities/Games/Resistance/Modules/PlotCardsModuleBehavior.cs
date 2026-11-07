namespace PartyKing.Domain.Entities.Games.Resistance.Modules;

/// <summary>The plot cards add-on.</summary>
internal sealed class PlotCardsModuleBehavior : IResistanceModuleBehavior
{
    public ResistanceModule Module => ResistanceModule.PlotCards;

    public ResistanceFailure LockedFailure => ResistanceFailure.PlotCardsLocked;

    public bool IsOn(ResistanceRules rules)
    {
        return rules.PlotCardsEnabled;
    }

    public void Switch(ResistanceRules rules, bool on)
    {
        rules.PlotCardsEnabled = on;
    }
}
