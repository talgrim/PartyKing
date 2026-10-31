namespace PartyKing.Contract.V1.Games.Ringo;

/// <summary>
/// The session's Ringo game: the seed of the card layout, the flips in flip order and the card (0-51) not continued
/// past yet, if any. <see cref="IsOpen"/> is whether guests may join, <see cref="Players"/> the players in turn order,
/// <see cref="CurrentPlayerId"/> the one whose turn it is (null without players) and <see cref="YourPlayerId"/> the
/// caller's own player id (null when the caller is not a player).
/// </summary>
public record RingoGameDto(
    int Seed,
    IReadOnlyList<RingoFlipDto> Flips,
    int? PendingCardId,
    bool IsOpen,
    IReadOnlyList<RingoPlayerDto> Players,
    Guid? CurrentPlayerId,
    Guid? YourPlayerId);
