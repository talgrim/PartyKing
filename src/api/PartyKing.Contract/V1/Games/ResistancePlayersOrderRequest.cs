namespace PartyKing.Contract.V1.Games;

/// <summary>The ids of all the players in the new seating order.</summary>
public record ResistancePlayersOrderRequest(IReadOnlyList<Guid>? PlayerIds);
