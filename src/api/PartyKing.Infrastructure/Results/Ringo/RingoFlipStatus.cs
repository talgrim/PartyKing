namespace PartyKing.Infrastructure.Results;

public enum RingoFlipStatus
{
    NoGame,

    /// <summary>The card was flipped and is now the pending one.</summary>
    Flipped,

    /// <summary>The card was already flipped, nothing changed.</summary>
    AlreadyFlipped,

    /// <summary>The fourth king was flipped, so the game is over and nothing changed.</summary>
    GameOver,

    /// <summary>Another card is still pending, nothing changed.</summary>
    CardPending,

    /// <summary>The caller is a player but it is another player's turn, nothing changed.</summary>
    NotYourTurn,
}
