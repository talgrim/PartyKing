namespace PartyKing.Domain.Entities.Games;

/// <summary>One flipped card of a Ringo game: who turned it and when.</summary>
public class RingoFlip
{
    public Guid SessionId { get; set; }

    /// <summary>Position in the flip order, from 0.</summary>
    public int Order { get; set; }

    /// <summary>Card id (0-51).</summary>
    public int CardId { get; set; }

    public DateTimeOffset FlippedAt { get; set; }

    public string FlippedBy { get; set; } = string.Empty;
}
