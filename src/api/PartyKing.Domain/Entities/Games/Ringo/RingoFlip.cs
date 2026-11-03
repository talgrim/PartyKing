namespace PartyKing.Domain.Entities.Games.Ringo;

/// <summary>One flipped card of a Ringo game: who turned it and when.</summary>
public class RingoFlip
{
    public Guid SessionId { get; internal set; }

    /// <summary>Position in the flip order, from 0.</summary>
    public int Order { get; internal set; }

    /// <summary>Card id (0-51).</summary>
    public int CardId { get; internal set; }

    public DateTimeOffset FlippedAt { get; internal set; }

    public string FlippedBy { get; internal set; } = string.Empty;
}
