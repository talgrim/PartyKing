namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>A seat of a started game that a guest may take over: the player is gone and the host or an admin released it.</summary>
public record ResistanceClaimableSeatDto(Guid Id, string Name);
