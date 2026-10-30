namespace PartyKing.Domain.Entities.Games;

/// <summary>Dealing the roles of a Resistance game.</summary>
public static class ResistanceRoles
{
    /// <summary>Missions one side has to win to win the game.</summary>
    public const int MissionsToWin = 3;

    /// <summary>Teams rejected in a row in one mission that give the spies the win.</summary>
    public const int MaxRejectedTeams = 5;

    /// <summary>A team is approved by a strict majority of all the players; a tie rejects it.</summary>
    public static bool IsApproved(ResistanceGame game) =>
        game.Players.Count(x => x.Vote == true) * 2 > game.Players.Count;

    /// <summary>The player after the leader in seating order, wrapping around.</summary>
    public static Guid NextLeader(ResistanceGame game)
    {
        var ordered = game.Players.OrderBy(x => x.Order).ToList();
        var index = ordered.FindIndex(x => x.PlayerId == game.LeaderPlayerId);

        return ordered[(index + 1) % ordered.Count].PlayerId;
    }

    /// <summary>
    /// Applies the complete vote: an approval resets the rejection count and starts the mission, a rejection passes
    /// the lead on, and the fifth one ends the game.
    /// </summary>
    public static void ResolveVotes(ResistanceGame game)
    {
        if (IsApproved(game))
        {
            game.RejectedTeams = 0;
            game.Phase = ResistancePhase.PlayingMission;
            return;
        }

        game.RejectedTeams++;
        if (game.RejectedTeams >= MaxRejectedTeams)
        {
            game.Phase = ResistancePhase.GameOver;
            game.Winner = ResistanceRole.Spy;
            return;
        }

        // The next leader picks at once; the votes stay (and are public) until that team is submitted
        game.LeaderPlayerId = NextLeader(game);
        game.Phase = ResistancePhase.SelectingTeam;
        foreach (var player in game.Players)
        {
            player.IsOnTeam = false;
        }
    }

    /// <summary>
    /// After a finished mission: the next player leads and picks again in the next mission, the team, the votes and the
    /// cards are cleared.
    /// </summary>
    public static void PassLeadership(ResistanceGame game)
    {
        game.MissionNumber++;
        game.LeaderPlayerId = NextLeader(game);
        game.Phase = ResistancePhase.SelectingTeam;
        foreach (var player in game.Players)
        {
            player.IsOnTeam = false;
            player.Vote = null;
            player.MissionCard = null;
        }
    }

    /// <summary>Whether every member of the approved team has played a card.</summary>
    public static bool AllCardsPlayed(ResistanceGame game) =>
        game.Players.Where(x => x.IsOnTeam).All(x => x.MissionCard is not null);

    /// <summary>
    /// Applies the complete set of cards: the mission fails when the Fail cards reach the rules' requirement, the
    /// outcome (the number of Fail cards only) is recorded, and the third success or failure ends the game.
    /// </summary>
    public static void ResolveMission(ResistanceGame game)
    {
        var fails = game.Players.Count(x => x.IsOnTeam && x.MissionCard == false);
        var succeeded = fails < game.Rules.RequiredFails(game.Players.Count, game.MissionNumber);

        game.MissionResults.Add(new ResistanceMissionResult
        {
            SessionId = game.SessionId,
            MissionNumber = game.MissionNumber,
            FailCount = fails,
            Succeeded = succeeded,
        });

        if (game.ResistanceScore >= MissionsToWin || game.SpyScore >= MissionsToWin)
        {
            game.Phase = ResistancePhase.GameOver;
            game.Winner = game.ResistanceScore >= MissionsToWin ? ResistanceRole.Resistance : ResistanceRole.Spy;
            return;
        }

        game.Phase = ResistancePhase.MissionResult;
    }

    /// <summary>Gives every player a random role and picks a random first leader.</summary>
    public static void Deal(ResistanceGame game, Random random)
    {
        var shuffled = game.Players.OrderBy(_ => random.Next()).ToList();
        var spies = game.Rules.SpyCount(shuffled.Count);

        for (var i = 0; i < shuffled.Count; i++)
        {
            shuffled[i].Role = i < spies ? ResistanceRole.Spy : ResistanceRole.Resistance;
        }

        game.LeaderPlayerId = game.Players[random.Next(game.Players.Count)].PlayerId;
    }
}
