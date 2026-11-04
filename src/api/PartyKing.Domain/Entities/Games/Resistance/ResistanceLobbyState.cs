namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>What the lobby of a Resistance game shows, without the game itself.</summary>
public sealed record ResistanceLobbyState(DateTimeOffset CreatedAt, bool IsOpen, bool IsStarted, bool Joined);
