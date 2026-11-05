namespace PartyKing.Domain.Events.Games.Resistance;

// The events of the Assassin module, like ResistanceGame.Assassin.cs. Same rule: no secrets in them.

/// <summary>The Assassin named a player as the Commander; the game is over, so who they named is public.</summary>
public sealed record ResistanceAssassinated(Guid SessionId, Guid AssassinPlayerId, Guid TargetPlayerId) : IDomainEvent;
