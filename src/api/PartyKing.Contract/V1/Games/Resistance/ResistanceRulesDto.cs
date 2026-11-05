namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>
/// The numbers the game was created with (copied from the server configuration, refreshed on request until the game
/// is open): for every player count the spies, the team size of each mission and the fails each mission needs.
/// </summary>
/// <param name="PlotCardsEnabled">Whether the plot cards add-on is played in this game.</param>
/// <param name="InquisitorEnabled">Whether the Inquisitor module is played in this game.</param>
/// <param name="BlindSpiesEnabled">Whether the Blind Spies variant is played in this game: the spies do not know each other.</param>
/// <param name="ReverserEnabled">Whether the Reverser module is played in this game: a Reverser on each side may play a Reverse card.</param>
/// <param name="ReverserSides">Which sides get a Reverser while the module is played.</param>
/// <param name="ReverserSpyKnowsSpies">Whether a spy Reverser and the other spies know each other; when false the Reverser spy keeps their eyes closed in the reveal.</param>
/// <param name="DefectorEnabled">Whether the Defector module is played in this game: a Defector on each side whose allegiance may switch from the third mission on.</param>
/// <param name="DefectorsKnowEachOther">Whether the two Defectors know each other while the module is played.</param>
/// <param name="AssassinEnabled">Whether the Assassin module is played in this game: a Commander and an Assassin, who names the Commander after the third successful mission.</param>
/// <param name="BodyGuardEnabled">Assassin module, optional: a Body Guard who knows the Commander (and the False Commander just as well).</param>
/// <param name="DeepCoverEnabled">Assassin module, optional: a spy the Commander does not see.</param>
/// <param name="BlindSpyEnabled">Assassin module, optional: a spy who knows no spy and is known to none.</param>
/// <param name="FalseCommanderEnabled">Assassin module, optional: a spy the Body Guard takes for the Commander.</param>
/// <param name="PlotCardsPerRound">How many plot cards the leader draws before each mission, at the current player count.</param>
public record ResistanceRulesDto(
    IReadOnlyList<ResistanceCountRulesDto> PlayerCounts, bool PlotCardsEnabled, int PlotCardsPerRound,
    bool InquisitorEnabled, bool BlindSpiesEnabled, bool ReverserEnabled, ResistanceReverserSidesDto ReverserSides,
    bool ReverserSpyKnowsSpies, bool DefectorEnabled, bool DefectorsKnowEachOther,
    bool AssassinEnabled, bool BodyGuardEnabled, bool DeepCoverEnabled, bool BlindSpyEnabled, bool FalseCommanderEnabled);
