namespace PartyKing.Contract.V1.Games;

/// <summary>A plot card the caller holds. <paramref name="ToDeal"/> is true while the leader still has to hand it out.</summary>
public record ResistancePlotCardDto(int Id, ResistancePlotCardTypeDto Type, bool ToDeal, bool InPlay);
