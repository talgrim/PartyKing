namespace PartyKing.Contract.V1.Games;

/// <summary>How many plot cards a player holds in hand; public, the cards themselves are not.</summary>
public record ResistancePlotCardCountDto(Guid PlayerId, int Count);
