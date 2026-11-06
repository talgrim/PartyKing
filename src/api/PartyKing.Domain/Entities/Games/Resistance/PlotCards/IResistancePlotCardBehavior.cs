namespace PartyKing.Domain.Entities.Games.Resistance.PlotCards;

/// <summary>
/// What sets one type of plot card apart from the rest: the facts the game branches on, so that a new card is one
/// class (or one line in <see cref="ResistancePlotCardBehaviors"/>) instead of an edit in every place that switches on
/// the type. The defaults describe a card kept in a hand and played in a window of its own; what a card does once it
/// is played stays in the <see cref="ResistanceGame"/> method of its action.
/// </summary>
internal interface IResistancePlotCardBehavior
{
    ResistancePlotCardType Type { get; }

    /// <summary>Whether the card is played while the immediate cards are resolved and its receiver confirms what they saw.</summary>
    bool IsImmediate => false;

    /// <summary>Whether the leader hands a drawn card to another player; false when the leader plays it on themselves.</summary>
    bool IsDealtByLeader => true;

    /// <summary>Whether the card is played the moment it is dealt and stays face up instead of going to a hand.</summary>
    bool StartsInPlay => false;

    /// <summary>
    /// Whether a card of this type in a hand opens the confirmation windows. They open for any such card, so the
    /// window never tells which card is out there.
    /// </summary>
    bool OpensWindows => true;

    /// <summary>Whether the card is played on a target by the play action (immediate cards, In the Spotlight).</summary>
    bool IsPlayedOnATarget => IsImmediate;

    /// <summary>The phase the play action accepts this card in.</summary>
    ResistancePhase PlayPhase => ResistancePhase.ResolvingCards;

    /// <summary>What the play action answers outside <see cref="PlayPhase"/>.</summary>
    ResistanceFailure WrongPlayPhase => ResistanceFailure.NotResolving;

    /// <summary>Whether playing the card needs nothing more from anyone, so it is confirmed when played.</summary>
    bool IsConfirmedOnPlay => false;

    /// <summary>Whether the game state allows playing the card now, beyond its phase and its target.</summary>
    bool CanBePlayed(ResistanceGame game)
    {
        return true;
    }

    /// <summary>Whether the target is one the play action accepts: another player.</summary>
    bool IsValidTarget(ResistanceGame game, ResistancePlotCard card, Guid targetPlayerId)
    {
        return targetPlayerId != card.HolderPlayerId && game.Players.Any(x => x.PlayerId == targetPlayerId);
    }

    /// <summary>The player who sees the identity card an immediate card shows: the target.</summary>
    Guid? Viewer(ResistancePlotCard card)
    {
        return card.TargetPlayerId;
    }

    /// <summary>The player whose identity card an immediate card shows: the holder.</summary>
    Guid? Revealed(ResistancePlotCard card)
    {
        return card.HolderPlayerId;
    }
}
