namespace PartyKing.Contract.V1.Games;

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

/// <summary>A flipped card (0-51), when it was flipped and by whom.</summary>
public record RingoFlipDto(int CardId, DateTimeOffset FlippedAt, string FlippedBy);

/// <summary>A player of the game; <see cref="Name"/> carries the "(host)" suffix for the session host.</summary>
public record RingoPlayerDto(Guid Id, string Name);

/// <summary>What any guest may know of the game: whether it is open to join and whether the caller already did.</summary>
public record RingoLobbyDto(int Seed, bool IsOpen, bool Joined);

public record RingoJoinRequest(string? Name);

/// <summary>The ids of all the players in the new turn order.</summary>
public record RingoPlayersOrderRequest(IReadOnlyList<Guid>? PlayerIds);
