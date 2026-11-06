namespace PartyKing.Domain.Entities.Games.Resistance.PlotCards;

/// <summary>The leader plays it on themselves, so it is not handed out when dealing.</summary>
internal sealed class EstablishConfidenceBehavior : IImmediateBehavior
{
    public ResistancePlotCardType Type => ResistancePlotCardType.EstablishConfidence;

    public bool IsDealtByLeader => false;
}
