namespace PartyKing.Contract.V1.Games;

/// <summary>The identity card a played card shows the caller, until they confirm having seen it.</summary>
public record ResistanceCardRevealDto(int CardId, Guid PlayerId, ResistanceRoleDto Role);
