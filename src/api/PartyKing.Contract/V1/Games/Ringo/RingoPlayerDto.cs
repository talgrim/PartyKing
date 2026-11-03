namespace PartyKing.Contract.V1.Games.Ringo;

/// <summary>A player of the game; <see cref="Name"/> carries the "(host)" suffix for the session host.</summary>
public record RingoPlayerDto(Guid Id, string Name);
