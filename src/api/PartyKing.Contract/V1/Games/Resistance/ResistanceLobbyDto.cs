namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>
/// What any guest may know of the game: whether it is open to join, whether it started (nobody can join then) and
/// whether the caller already joined.
/// </summary>
public record ResistanceLobbyDto(DateTimeOffset CreatedAt, bool IsOpen, bool IsStarted, bool Joined);
