namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>The player an immediate plot card is played on.</summary>
public record ResistancePlayPlotCardRequest(Guid TargetPlayerId);
