namespace PartyKing.Infrastructure.Results;

public enum ResistanceDealStatus
{
    NoGame,
    NotStarted,
    GameOver,

    /// <summary>The caller is not the player leading the mission.</summary>
    NotLeader,

    /// <summary>No cards are waiting to be handed out (add-on off, or already dealt).</summary>
    NotDealing,

    /// <summary>A card that was not drawn, is kept by the leader, or is given twice.</summary>
    UnknownCard,

    /// <summary>A receiver that is not a player, or the leader themselves.</summary>
    InvalidReceiver,

    /// <summary>Some of the cards to hand out are left.</summary>
    Incomplete,
    Dealt,
}
