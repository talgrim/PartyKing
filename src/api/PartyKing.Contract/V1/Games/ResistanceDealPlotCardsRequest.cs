namespace PartyKing.Contract.V1.Games;

/// <summary>Every drawn card except Establish Confidence (the leader keeps it) goes to a player other than the leader.</summary>
public record ResistanceDealPlotCardsRequest(IReadOnlyList<ResistancePlotCardAssignmentDto> Assignments);
