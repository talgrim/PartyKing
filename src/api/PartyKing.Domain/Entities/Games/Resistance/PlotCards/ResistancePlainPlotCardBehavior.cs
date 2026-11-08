namespace PartyKing.Domain.Entities.Games.Resistance.PlotCards;

/// <summary>A card that sets itself apart in none of the facts of <see cref="IResistancePlotCardBehavior"/>.</summary>
internal sealed class ResistancePlainPlotCardBehavior : IResistancePlotCardBehavior
{
    public ResistancePlainPlotCardBehavior(ResistancePlotCardType type)
    {
        Type = type;
    }

    public ResistancePlotCardType Type { get; }
}
