namespace PartyKing.Contract.V1.Games;

/// <summary>What any guest may know of the game: whether it is open to join and whether the caller already did.</summary>
public record RingoLobbyDto(int Seed, bool IsOpen, bool Joined);
