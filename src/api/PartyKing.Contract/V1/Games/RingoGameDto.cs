namespace PartyKing.Contract.V1.Games;

/// <summary>The session's Ringo game: the seed of the card layout and the flipped card ids (0-51) in flip order.</summary>
public record RingoGameDto(int Seed, IReadOnlyList<int> FlippedCards);
