namespace PartyKing.Contract.V1.Games;

/// <summary>The ids of all the players in the new turn order.</summary>
public record RingoPlayersOrderRequest(IReadOnlyList<Guid>? PlayerIds);
