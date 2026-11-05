namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>A mission card the caller looked at with Keeping a Close Eye; private to the peeker.</summary>
public record ResistanceMissionPeekDto(Guid PlayerId, ResistanceMissionCardKindDto Card);
