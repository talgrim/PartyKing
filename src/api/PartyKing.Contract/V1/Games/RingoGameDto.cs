namespace PartyKing.Contract.V1.Games;

/// <summary>
/// The session's Ringo game: the seed of the card layout, the flips in flip order and the card (0-51) the host or an
/// admin has not continued past yet, if any.
/// </summary>
public record RingoGameDto(int Seed, IReadOnlyList<RingoFlipDto> Flips, int? PendingCardId);

/// <summary>A flipped card (0-51), when it was flipped and by whom.</summary>
public record RingoFlipDto(int CardId, DateTimeOffset FlippedAt, string FlippedBy);
