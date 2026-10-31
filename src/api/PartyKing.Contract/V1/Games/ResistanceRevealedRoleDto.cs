namespace PartyKing.Contract.V1.Games;

/// <summary>A player's role, revealed when the game is over.</summary>
public record ResistanceRevealedRoleDto(Guid PlayerId, ResistanceRoleDto Role);
