namespace PartyKing.Domain.Entities.Games;

/// <summary>The Ringo game of one session (at most one): the seed of the card layout and the flipped cards in flip order.</summary>
public class RingoGame
{
    public Guid SessionId { get; set; }
    public int Seed { get; set; }

    /// <summary>Card ids (0-51) in the order they were flipped, comma separated.</summary>
    public string FlippedCards { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
