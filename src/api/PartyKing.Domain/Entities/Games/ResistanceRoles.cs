namespace PartyKing.Domain.Entities.Games;

/// <summary>Dealing the roles of a Resistance game.</summary>
public static class ResistanceRoles
{
    /// <summary>A third of the players, rounded up: 2, 2, 3, 3, 3, 4 spies for 5 to 10 players.</summary>
    public static int SpyCount(int playerCount) => (playerCount + 2) / 3;

    /// <summary>Team sizes of the five missions, by player count (5 to 10).</summary>
    private static readonly Dictionary<int, int[]> TeamSizes = new()
    {
        [5] = [2, 3, 2, 3, 3],
        [6] = [2, 3, 4, 3, 4],
        [7] = [2, 3, 3, 4, 4],
        [8] = [3, 4, 4, 5, 5],
        [9] = [3, 4, 4, 5, 5],
        [10] = [3, 4, 4, 5, 5],
    };

    /// <summary>How many players the leader sends on the mission (1 to 5), 5 to 10 players.</summary>
    public static int TeamSize(int playerCount, int missionNumber) =>
        TeamSizes[Math.Clamp(playerCount, ResistanceGame.MinPlayers, ResistanceGame.MaxPlayers)][missionNumber - 1];

    /// <summary>Gives every player a random role and picks a random first leader.</summary>
    public static void Deal(ResistanceGame game, Random random)
    {
        var shuffled = game.Players.OrderBy(_ => random.Next()).ToList();
        var spies = SpyCount(shuffled.Count);

        for (var i = 0; i < shuffled.Count; i++)
        {
            shuffled[i].Role = i < spies ? ResistanceRole.Spy : ResistanceRole.Resistance;
        }

        game.LeaderPlayerId = game.Players[random.Next(game.Players.Count)].PlayerId;
    }
}
