namespace PartyKing.Domain.Entities.Games.Resistance.PlotCards;

/// <summary>Played the moment it is dealt and face up: its holder votes before the others.</summary>
internal sealed class OpinionMakerBehavior : IResistancePlotCardBehavior
{
    public ResistancePlotCardType Type => ResistancePlotCardType.OpinionMaker;

    public bool StartsInPlay => true;
}
