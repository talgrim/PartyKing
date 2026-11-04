namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>A player of the game; <see cref="Name"/> carries the "(host)" suffix for the session host.</summary>
/// <param name="IsSeatReleased">Whether the host or an admin released the seat for a guest to take over.</param>
/// <param name="IsManagerSeat">Whether the seat belongs to the session host or to an admin, which are never released.</param>
public record ResistancePlayerDto(Guid Id, string Name, bool IsSeatReleased = false, bool IsManagerSeat = false);
