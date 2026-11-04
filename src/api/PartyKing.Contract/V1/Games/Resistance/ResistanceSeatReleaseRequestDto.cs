namespace PartyKing.Contract.V1.Games.Resistance;

/// <param name="Released">True releases the seat for a guest to take over, false takes the release back.</param>
public record ResistanceSeatReleaseRequestDto(bool Released);
