using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>Dealing the sides of a Resistance game when it starts, and clearing it when it restarts; what the modules deal is in their behaviors.</summary>
public static class ResistanceDealer
{
    /// <summary>Clears everything a game accumulated (results, rejections, teams, votes, cards, winner) but keeps the players and rules.</summary>
    public static void Reset(ResistanceGame game)
    {
        game.MissionResults.Clear();
        game.MissionNumber = 1;
        game.RejectedTeams = 0;
        game.Winner = null;
        game.Phase = ResistancePhase.SelectingTeam;
        foreach (var player in game.Players)
        {
            player.IsOnTeam = false;
            player.Vote = null;
            player.MissionCard = null;
            player.PassedLeaderWindow = false;
            player.Role = null;
            player.IsReclaimable = false;
            player.Character = ResistanceCharacter.Standard;
        }

        game.LeaderPlayerId = null;
        foreach (var module in ResistanceModules.Dealing)
        {
            module.Reset(game);
        }
    }

    /// <summary>
    /// Gives every player a random side, lets the modules deal their characters, picks a random first leader, lets the
    /// modules deal what depends on the leader and starts the first mission. The modules go in the order of
    /// <see cref="ResistanceModules.Registered"/> (see <see cref="Modules.ICharacterDealBehavior"/> and
    /// <see cref="Modules.IDealBehavior"/>). A game is dealt once, new or after <see cref="Reset"/>, so no module clears
    /// its own state here.
    /// </summary>
    public static void Deal(ResistanceGame game, Random random)
    {
        var shuffled = game.Players.Shuffled(random);
        var spies = game.Rules.SpyCount(shuffled.Count);

        for (var i = 0; i < shuffled.Count; i++)
        {
            shuffled[i].Role = i < spies ? ResistanceRole.Spy : ResistanceRole.Resistance;
            shuffled[i].Character = ResistanceCharacter.Standard;
        }

        foreach (var module in ResistanceModules.CharacterDealers)
        {
            module.DealCharacters(game, shuffled, random);
        }

        game.ChangeLeader(game.Players[random.Next(game.Players.Count)].PlayerId);
        foreach (var module in ResistanceModules.Dealing)
        {
            module.DealTable(game, random);
        }

        ResistanceFlow.BeginMission(game);
    }
}
