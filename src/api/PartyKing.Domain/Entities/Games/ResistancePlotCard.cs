namespace PartyKing.Domain.Entities.Games;

/// <summary>Where a plot card is.</summary>
public enum ResistancePlotCardLocation
{
    /// <summary>Face down in the deck, in <see cref="ResistancePlotCard.DeckOrder"/>.</summary>
    Deck,

    /// <summary>The leader drew it and has to hand it to another player; only the leader sees it.</summary>
    Drawn,

    /// <summary>In the hand of <see cref="ResistancePlotCard.HolderPlayerId"/>, hidden from everyone else.</summary>
    Hand,

    /// <summary>
    /// Face up in front of <see cref="ResistancePlotCard.HolderPlayerId"/> for everyone to see (an Opinion Maker, which
    /// is played the moment it is dealt and stays for the rest of the game).
    /// </summary>
    InPlay,
}

/// <summary>One card of the plot cards add-on, built from <see cref="ResistanceRules.PlotDeck"/> when the game starts.</summary>
public class ResistancePlotCard
{
    public Guid SessionId { get; set; }

    /// <summary>Identifies the card within the game (0 to 14).</summary>
    public int CardId { get; set; }

    public ResistancePlotCardType Type { get; set; }

    public ResistancePlotCardLocation Location { get; set; }

    /// <summary>The player holding the card while it is <see cref="ResistancePlotCardLocation.Drawn"/> or in a hand.</summary>
    public Guid? HolderPlayerId { get; set; }

    /// <summary>The position in the shuffled deck; the lowest is drawn first.</summary>
    public int DeckOrder { get; set; }
}
