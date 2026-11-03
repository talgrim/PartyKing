namespace PartyKing.Domain.Entities.Games.Ringo;

/// <summary>Why a Ringo action did not happen. A card that is flipped already is not a failure, see <see cref="RingoOutcome"/>.</summary>
public enum RingoFailure
{
    NoGame,

    /// <summary>The caller is a player but it is another player's turn.</summary>
    NotYourTurn,

    /// <summary>The fourth king was flipped, so nothing can be flipped any more.</summary>
    GameOver,

    /// <summary>Another card is still pending: continue past it first.</summary>
    CardPending,
}
