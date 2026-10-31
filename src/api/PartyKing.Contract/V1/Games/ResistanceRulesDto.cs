namespace PartyKing.Contract.V1.Games;

/// <summary>
/// The numbers the game was created with (copied from the server configuration, refreshed on request until the game
/// is open): for every player count the spies, the team size of each mission and the fails each mission needs.
/// </summary>
/// <param name="PlotCardsEnabled">Whether the plot cards add-on is played in this game.</param>
/// <param name="PlotCardsPerRound">How many plot cards the leader draws before each mission, at the current player count.</param>
public record ResistanceRulesDto(
    IReadOnlyList<ResistanceCountRulesDto> PlayerCounts, bool PlotCardsEnabled, int PlotCardsPerRound);
