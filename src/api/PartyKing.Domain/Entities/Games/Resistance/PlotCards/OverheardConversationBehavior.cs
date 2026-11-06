namespace PartyKing.Domain.Entities.Games.Resistance.PlotCards;

/// <summary>The holder looks at the identity card of a neighbour.</summary>
internal sealed class OverheardConversationBehavior : IResistancePlotCardBehavior
{
    public ResistancePlotCardType Type => ResistancePlotCardType.OverheardConversation;

    public bool IsImmediate => true;

    public bool IsValidTarget(ResistanceGame game, ResistancePlotCard card, Guid targetPlayerId)
    {
        return ResistancePlotCardQueries.Neighbours(game, card.HolderPlayerId!.Value).Contains(targetPlayerId);
    }

    public Guid? Viewer(ResistancePlotCard card)
    {
        return card.HolderPlayerId;
    }

    public Guid? Revealed(ResistancePlotCard card)
    {
        return card.TargetPlayerId;
    }
}
