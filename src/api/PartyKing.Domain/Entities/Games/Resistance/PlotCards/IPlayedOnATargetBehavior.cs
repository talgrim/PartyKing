namespace PartyKing.Domain.Entities.Games.Resistance.PlotCards;

/// <summary>A card played with the play action on a target (immediate cards and In the Spotlight).</summary>
internal interface IPlayedOnATargetBehavior : IResistancePlotCardBehavior
{
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
}
