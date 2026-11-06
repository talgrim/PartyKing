namespace PartyKing.Domain.Entities.Games.Resistance.PlotCards;

/// <summary>The holder shows their identity card to another player.</summary>
internal sealed class OpenUpBehavior : IImmediateBehavior
{
    public ResistancePlotCardType Type => ResistancePlotCardType.OpenUp;
}
