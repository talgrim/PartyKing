namespace PartyKing.Contract.V1.Games;

/// <summary>A mission card the caller looked at with Keeping a Close Eye (true = Success); private to the peeker.</summary>
public record ResistanceMissionPeekDto(Guid PlayerId, bool Success);
