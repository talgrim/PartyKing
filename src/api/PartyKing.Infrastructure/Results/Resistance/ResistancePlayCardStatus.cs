namespace PartyKing.Infrastructure.Results;

public enum ResistancePlayCardStatus
{
    NoGame,
    NotStarted,
    GameOver,

    /// <summary>The immediate cards are not being played.</summary>
    NotResolving,

    /// <summary>In the Spotlight is played while the mission cards are played, and the mission is not.</summary>
    NotPlayingMission,

    /// <summary>No such card, or it is not an immediate card.</summary>
    UnknownCard,

    /// <summary>The caller does not hold the card (to play it), or is not the one it shows the identity card to (to confirm it).</summary>
    NotAllowed,

    /// <summary>The card was played already (to play it), or has not been played yet (to confirm it).</summary>
    WrongState,

    /// <summary>The target is not a legal one for the card.</summary>
    InvalidTarget,

    /// <summary>The card can't be played in this step of the round.</summary>
    WrongPhase,

    /// <summary>The caller did not join the game (a host or admin who only watches).</summary>
    NotPlayer,

    /// <summary>The target has a face-up Opinion Maker and cards in hand, and the caller did not say which to take.</summary>
    ChoiceRequired,
    Done,
}
