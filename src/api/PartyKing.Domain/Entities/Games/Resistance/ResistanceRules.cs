namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>
/// The numbers of a Resistance game by player count (5 to 10). The server configuration builds them, and a game keeps
/// its own copy (<see cref="ResistanceGame.Rules"/>) so a configuration change never alters a game in progress.
/// </summary>
public class ResistanceRules
{
    public const int Missions = 5;

    public Dictionary<int, ResistanceCountRules> PlayerCounts { get; set; } = new();

    /// <summary>Whether the plot cards add-on is played. Off by default; only editable before the game is opened.</summary>
    public bool PlotCardsEnabled { get; set; }

    /// <summary>Whether the Inquisitor module (Hostile Intent) is played. Off by default; only editable before the game starts.</summary>
    public bool InquisitorEnabled { get; set; }

    /// <summary>
    /// Whether the Blind Spies variant is played: the spies do not get to know each other. Off by default; only editable
    /// before the game starts.
    /// </summary>
    public bool BlindSpiesEnabled { get; set; }

    /// <summary>
    /// Whether the Reverser module is played: a Reverser on each side may play a Reverse card, which turns the result of
    /// the mission around. Off by default; only editable before the game starts.
    /// </summary>
    public bool ReverserEnabled { get; set; }

    /// <summary>Which sides get a Reverser while <see cref="ReverserEnabled"/>. Both by default; only editable before the game starts.</summary>
    public ResistanceReverserSides ReverserSides { get; set; }

    /// <summary>
    /// Whether a spy Reverser takes part in the spies' reveal while <see cref="ReverserEnabled"/>: they know the other
    /// spies and the other spies know them. When off, the Reverser spy keeps their eyes closed, so neither knows the
    /// other. On by default (also for rules saved before this setting existed); only editable before the game starts.
    /// </summary>
    public bool ReverserSpyKnowsSpies { get; set; } = true;

    /// <summary>
    /// Whether the Defector module (Hidden Agenda) is played: a Defector on each side, whose allegiance may switch at
    /// the start of the missions 3 to 5. Off by default; only editable before the game starts.
    /// </summary>
    public bool DefectorEnabled { get; set; }

    /// <summary>
    /// Whether the two Defectors know each other while <see cref="DefectorEnabled"/> (the variant for large groups).
    /// Off by default; only editable before the game starts.
    /// </summary>
    public bool DefectorsKnowEachOther { get; set; }

    /// <summary>
    /// Whether the Assassin module (Hidden Agenda) is played: a Commander and an Assassin replace one standard player on
    /// each side, and after the third successful mission the Assassin names the Commander. Off by default; only
    /// editable before the game starts, and not together with the Defector, the Reverser or Blind Spies.
    /// </summary>
    public bool AssassinEnabled { get; set; }

    /// <summary>Assassin module, optional character: a Body Guard among the resistance. Only while <see cref="AssassinEnabled"/>.</summary>
    public bool BodyGuardEnabled { get; set; }

    /// <summary>Assassin module, optional character: a spy the Commander does not see. Only while <see cref="AssassinEnabled"/>.</summary>
    public bool DeepCoverEnabled { get; set; }

    /// <summary>Assassin module, optional character: a spy who knows no spy and is known to none. Only while <see cref="AssassinEnabled"/>.</summary>
    public bool BlindSpyEnabled { get; set; }

    /// <summary>Assassin module, optional character: a spy the Body Guard takes for the Commander. Only while <see cref="AssassinEnabled"/>.</summary>
    public bool FalseCommanderEnabled { get; set; }

    /// <summary>How many of the optional spy characters (Deep Cover, Blind Spy, False Commander) are switched on.</summary>
    public int OptionalSpyCharacters => (DeepCoverEnabled ? 1 : 0) + (BlindSpyEnabled ? 1 : 0) + (FalseCommanderEnabled ? 1 : 0);

    /// <summary>How many copies of each plot card the deck holds (15 cards in all).</summary>
    public Dictionary<ResistancePlotCardType, int> PlotDeck { get; set; } = DefaultPlotDeck();

    /// <summary>
    /// A deep copy, so a game never shares dictionaries or arrays with the object it was built from. The result is
    /// always a new instance, which the JSON column needs to detect a change. Every property must be copied here.
    /// </summary>
    public ResistanceRules Clone(
        bool? plotCardsEnabled = null,
        bool? inquisitorEnabled = null,
        bool? blindSpiesEnabled = null,
        bool? reverserEnabled = null,
        ResistanceReverserSides? reverserSides = null,
        bool? reverserSpyKnowsSpies = null,
        bool? defectorEnabled = null,
        bool? defectorsKnowEachOther = null,
        bool? assassinEnabled = null,
        bool? bodyGuardEnabled = null,
        bool? deepCoverEnabled = null,
        bool? blindSpyEnabled = null,
        bool? falseCommanderEnabled = null) => new()
    {
        PlayerCounts = PlayerCounts.ToDictionary(
            x => x.Key, x => new ResistanceCountRules(x.Value.Spies, [.. x.Value.TeamSizes], [.. x.Value.RequiredFails])),
        PlotCardsEnabled = plotCardsEnabled ?? PlotCardsEnabled,
        InquisitorEnabled = inquisitorEnabled ?? InquisitorEnabled,
        BlindSpiesEnabled = blindSpiesEnabled ?? BlindSpiesEnabled,
        ReverserEnabled = reverserEnabled ?? ReverserEnabled,
        ReverserSides = reverserSides ?? ReverserSides,
        ReverserSpyKnowsSpies = reverserSpyKnowsSpies ?? ReverserSpyKnowsSpies,
        DefectorEnabled = defectorEnabled ?? DefectorEnabled,
        DefectorsKnowEachOther = defectorsKnowEachOther ?? DefectorsKnowEachOther,
        AssassinEnabled = assassinEnabled ?? AssassinEnabled,
        BodyGuardEnabled = bodyGuardEnabled ?? BodyGuardEnabled,
        DeepCoverEnabled = deepCoverEnabled ?? DeepCoverEnabled,
        BlindSpyEnabled = blindSpyEnabled ?? BlindSpyEnabled,
        FalseCommanderEnabled = falseCommanderEnabled ?? FalseCommanderEnabled,
        PlotDeck = new Dictionary<ResistancePlotCardType, int>(PlotDeck),
    };

    /// <summary>A third of the players as spies, rounded up; the official team sizes; two fails on mission 4 from 7 players.</summary>
    public static ResistanceRules Default() => new()
    {
        PlayerCounts = new Dictionary<int, ResistanceCountRules>
        {
            [5] = new(2, [2, 3, 2, 3, 3], [1, 1, 1, 1, 1]),
            [6] = new(2, [2, 3, 4, 3, 4], [1, 1, 1, 1, 1]),
            [7] = new(3, [2, 3, 3, 4, 4], [1, 1, 1, 2, 1]),
            [8] = new(3, [3, 4, 4, 5, 5], [1, 1, 1, 2, 1]),
            [9] = new(3, [3, 4, 4, 5, 5], [1, 1, 1, 2, 1]),
            [10] = new(4, [3, 4, 4, 5, 5], [1, 1, 1, 2, 1]),
        },
    };

    public static Dictionary<ResistancePlotCardType, int> DefaultPlotDeck() => new()
    {
        [ResistancePlotCardType.OverheardConversation] = 2,
        [ResistancePlotCardType.OpinionMaker] = 2,
        [ResistancePlotCardType.EstablishConfidence] = 1,
        [ResistancePlotCardType.OpenUp] = 1,
        [ResistancePlotCardType.NoConfidence] = 3,
        [ResistancePlotCardType.KeepingCloseEye] = 2,
        [ResistancePlotCardType.StrongLeader] = 2,
        [ResistancePlotCardType.InTheSpotlight] = 1,
        [ResistancePlotCardType.TakeResponsibility] = 1,
    };

    /// <summary>How many plot cards the leader draws before each mission: 1 for 5-6 players, 2 for 7-8, 3 for 9-10.</summary>
    public int PlotCardsPerRound(int playerCount) =>
        (Math.Clamp(playerCount, ResistanceGame.MinPlayers, ResistanceGame.MaxPlayers) - 3) / 2;

    public int SpyCount(int playerCount) => For(playerCount).Spies;

    /// <summary>How many players the leader sends on the mission (1 to 5).</summary>
    public int TeamSize(int playerCount, int missionNumber) => For(playerCount).TeamSizes[missionNumber - 1];

    /// <summary>How many Fail cards make the mission fail.</summary>
    public int RequiredFails(int playerCount, int missionNumber) => For(playerCount).RequiredFails[missionNumber - 1];

    private ResistanceCountRules For(int playerCount) =>
        PlayerCounts[Math.Clamp(playerCount, ResistanceGame.MinPlayers, ResistanceGame.MaxPlayers)];
}
