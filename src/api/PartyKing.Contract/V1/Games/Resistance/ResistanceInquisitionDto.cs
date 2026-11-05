namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>The loyalty of the player the Inquisitor examined (spy or resistance); the Inquisitor may say anything about it.</summary>
public record ResistanceInquisitionDto(Guid PlayerId, ResistanceRoleDto Role);
