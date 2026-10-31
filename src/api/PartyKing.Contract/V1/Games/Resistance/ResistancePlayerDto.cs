namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>A player of the game; <see cref="Name"/> carries the "(host)" suffix for the session host.</summary>
public record ResistancePlayerDto(Guid Id, string Name);
