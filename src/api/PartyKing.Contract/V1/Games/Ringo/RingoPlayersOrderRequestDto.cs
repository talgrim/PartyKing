namespace PartyKing.Contract.V1.Games.Ringo;

/// <summary>The ids of all the players in the new turn order.</summary>
public record RingoPlayersOrderRequestDto(IReadOnlyList<Guid>? PlayerIds);
