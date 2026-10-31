namespace PartyKing.Contract.V1.Games;

/// <summary>A flipped card (0-51), when it was flipped and by whom.</summary>
public record RingoFlipDto(int CardId, DateTimeOffset FlippedAt, string FlippedBy);
