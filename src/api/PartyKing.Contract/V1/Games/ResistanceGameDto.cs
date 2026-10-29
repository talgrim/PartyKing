namespace PartyKing.Contract.V1.Games;

/// <summary>
/// The session's Resistance game. <see cref="CreatedAt"/> tells one game from the next, <see cref="IsOpen"/> is whether
/// guests may join, <see cref="Players"/> the players in seating order and <see cref="YourPlayerId"/> the caller's own
/// player id (null when the caller only watches). <see cref="MaxPlayers"/> is how many may join.
/// </summary>
public record ResistanceGameDto(
    DateTimeOffset CreatedAt,
    bool IsOpen,
    IReadOnlyList<ResistancePlayerDto> Players,
    Guid? YourPlayerId,
    int MaxPlayers);

/// <summary>A player of the game; <see cref="Name"/> carries the "(host)" suffix for the session host.</summary>
public record ResistancePlayerDto(Guid Id, string Name);

/// <summary>What any guest may know of the game: whether it is open to join and whether the caller already did.</summary>
public record ResistanceLobbyDto(DateTimeOffset CreatedAt, bool IsOpen, bool Joined);

public record ResistanceJoinRequest(string? Name);

/// <summary>The ids of all the players in the new seating order.</summary>
public record ResistancePlayersOrderRequest(IReadOnlyList<Guid>? PlayerIds);
