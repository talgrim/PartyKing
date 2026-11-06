namespace PartyKing.Domain.Entities.Games.Resistance.PlotCards;

/// <summary>
/// What sets one type of plot card apart in the way it is dealt. The defaults describe an ordinary card, dealt by the
/// leader into a hand, so a type that sets itself apart in nothing needs no behavior of its own
/// (<see cref="ResistancePlotCardBehaviors.For"/> answers a plain one). A card that is played with the play action on
/// a target also implements <see cref="IPlayedOnATargetBehavior"/>, and one that shows an identity card
/// <see cref="IImmediateBehavior"/>. What a card does once it is played stays in the <see cref="ResistanceGame"/>
/// method of its action.
/// </summary>
internal interface IResistancePlotCardBehavior
{
    ResistancePlotCardType Type { get; }

    /// <summary>Whether the leader hands a drawn card to another player; false when the leader plays it on themselves.</summary>
    bool IsDealtByLeader => true;

    /// <summary>Whether the card is played the moment it is dealt and stays face up instead of going to a hand.</summary>
    bool StartsInPlay => false;
}
