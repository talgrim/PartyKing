namespace PartyKing.Domain.Entities.Games;

/// <summary>Dealing the roles of a Resistance game.</summary>
public static class ResistanceRoles
{
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

    /// <summary>Applies the complete vote: an approval resets the rejection count, the fifth rejection ends the game.</summary>
    public static void ResolveVotes(ResistanceGame game)
    {
        if (IsApproved(game))
        {
            game.RejectedTeams = 0;
            game.Phase = ResistancePhase.VoteResult;
            return;
        }

        game.RejectedTeams++;
        if (game.RejectedTeams >= MaxRejectedTeams)
        {
            game.Phase = ResistancePhase.GameOver;
            game.Winner = ResistanceRole.Spy;
            return;
        }

        game.Phase = ResistancePhase.VoteResult;
    }

    /// <summary>After a rejected team: the next player leads and picks again, the team and the votes are cleared.</summary>
    public static void PassLeadership(ResistanceGame game)
    {
        game.LeaderPlayerId = NextLeader(game);
        game.Phase = ResistancePhase.SelectingTeam;
        foreach (var player in game.Players)
        {
            player.IsOnTeam = false;
            player.Vote = null;
        }
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
