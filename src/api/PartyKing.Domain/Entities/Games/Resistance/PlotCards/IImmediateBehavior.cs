namespace PartyKing.Domain.Entities.Games.Resistance.PlotCards;

/// <summary>
/// A card played while the immediate cards are resolved, which shows an identity card to its receiver, who confirms
/// what they saw.
/// </summary>
internal interface IImmediateBehavior : IPlayedOnATargetBehavior
{
    /// <summary>The player who sees the identity card the card shows: the target.</summary>
    Guid? Viewer(ResistancePlotCard card)
    {
        return card.TargetPlayerId;
    }

    /// <summary>The player whose identity card the card shows: the holder.</summary>
    Guid? Revealed(ResistancePlotCard card)
    {
        return card.HolderPlayerId;
    }
}
