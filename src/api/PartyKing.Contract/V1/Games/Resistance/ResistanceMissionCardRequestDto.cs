namespace PartyKing.Contract.V1.Games.Resistance;

/// <param name="Card">The card to play: Success, Fail or (a Reverser only) Reverse.</param>
public record ResistanceMissionCardRequestDto(ResistanceMissionCardKindDto Card);
