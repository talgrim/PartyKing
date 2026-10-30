namespace PartyKing.Domain.Entities.Games;

/// <summary>The rules for one player count: spies, team size and fails needed for each of the five missions.</summary>
public record ResistanceCountRules(int Spies, int[] TeamSizes, int[] RequiredFails);

/// <summary>
/// The numbers of a Resistance game by player count (5 to 10). The server configuration builds them, and a game keeps
/// its own copy (<see cref="ResistanceGame.Rules"/>) so a configuration change never alters a game in progress.
/// </summary>
public class ResistanceRules
{
    public const int Missions = 5;

    public Dictionary<int, ResistanceCountRules> PlayerCounts { get; set; } = new();

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

    public int SpyCount(int playerCount) => For(playerCount).Spies;

    /// <summary>How many players the leader sends on the mission (1 to 5).</summary>
    public int TeamSize(int playerCount, int missionNumber) => For(playerCount).TeamSizes[missionNumber - 1];

    /// <summary>How many Fail cards make the mission fail.</summary>
    public int RequiredFails(int playerCount, int missionNumber) => For(playerCount).RequiredFails[missionNumber - 1];

    private ResistanceCountRules For(int playerCount) =>
        PlayerCounts[Math.Clamp(playerCount, ResistanceGame.MinPlayers, ResistanceGame.MaxPlayers)];
}
