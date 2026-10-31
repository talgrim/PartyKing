namespace PartyKing.Infrastructure.Results;

/// <summary>The leader gives the card with this id to the player.</summary>
public record PlotCardAssignment(int CardId, Guid PlayerId);
