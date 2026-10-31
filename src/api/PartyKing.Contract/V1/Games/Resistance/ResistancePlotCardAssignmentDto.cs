namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>The leader gives the card with this id to the player.</summary>
public record ResistancePlotCardAssignmentDto(int CardId, Guid PlayerId);
