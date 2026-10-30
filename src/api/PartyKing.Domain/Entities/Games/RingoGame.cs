namespace PartyKing.Domain.Entities.Games;

/// <summary>The Ringo game of one session (at most one): the seed of the card layout and the flipped cards in flip order.</summary>
public class RingoGame : LobbyGame<RingoPlayer>
{
    public int Seed { get; set; }

    /// <summary>The last flipped card until the host or an admin continues; null when none awaits that.</summary>
    public int? PendingCardId { get; set; }

    /// <summary>The player whose turn it is; null while nobody has joined.</summary>
    public Guid? CurrentPlayerId { get; set; }

    public List<RingoFlip> Flips { get; set; } = [];

    /// <summary>The fourth king ends the game: nothing can be flipped any more.</summary>
    public bool IsOver => Flips.Count(x => x.CardId % 13 == KingRankIndex) >= KingsToEnd;

    public const int KingsToEnd = 4;

    // Card ids run by suit, 13 ranks each, 2 to ace, so the king is the rank at index 11
    private const int KingRankIndex = 11;
}
