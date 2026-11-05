namespace PartyKing.Domain.Events.Games.Resistance;

// The events of the Inquisitor module, like ResistanceGame.Inquisitor.cs. Same rule: no secrets in them.

/// <summary>The Inquisitor picked a player to examine and the token passed to them; what was learned is not told.</summary>
public sealed record ResistanceInquisitorTokenPassed(Guid SessionId, Guid FromPlayerId, Guid ToPlayerId) : IDomainEvent;
