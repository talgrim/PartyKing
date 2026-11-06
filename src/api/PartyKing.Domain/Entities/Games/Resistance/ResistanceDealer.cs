using PartyKing.Domain.Entities.Games.Base;

namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>Dealing the roles, characters and plot cards of a Resistance game when it starts, and clearing it when it restarts.</summary>
public static class ResistanceDealer
{
    /// <summary>
    /// Builds and shuffles the deck from the rules' composition, or clears it when the add-on is off. The deck holds at
    /// most 15 cards and a game draws at most 5 missions x 3 cards, so it never runs out.
    /// </summary>
    public static void BuildPlotDeck(ResistanceGame game, Random random)
    {
        if (!game.Rules.PlotCardsEnabled)
        {
            game.PlotCards.Clear();
            return;
        }

        var types = game.Rules.PlotDeck
            .OrderBy(x => x.Key)
            .SelectMany(x => Enumerable.Repeat(x.Key, x.Value))
            .ToList();
        var order = Enumerable.Range(0, types.Count).Shuffled(random);

        // A restart reuses the rows (same keys), because deleting and re-adding a key in one save conflicts
        game.PlotCards.RemoveAll(x => x.CardId >= types.Count);
        for (var i = 0; i < types.Count; i++)
        {
            var card = game.PlotCards.Find(x => x.CardId == i);
            if (card is null)
            {
                card = new ResistancePlotCard { SessionId = game.SessionId, CardId = i };
                game.PlotCards.Add(card);
            }

            card.Type = types[i];
            card.Location = ResistancePlotCardLocation.Deck;
            card.HolderPlayerId = null;
            card.DeckOrder = order[i];
            card.TargetPlayerId = null;
            card.PlayedAt = null;
            card.PlayedInMission = 0;
            card.IsConfirmed = false;
        }
    }

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
            player.WasInquisitor = false;
            player.Character = ResistanceCharacter.Standard;
        }

        game.LeaderPlayerId = null;
        game.InquisitorPlayerId = null;
        game.InquisitionExaminerPlayerId = null;
        game.PlotCards.Clear();
        game.DefectorDeck = string.Empty;
        game.AssassinationTargetPlayerId = null;
    }

    /// <summary>The token starts with the player to the right of the first leader: the one before them in seating order.</summary>
    private static void GiveInquisitorToken(ResistanceGame game)
    {
        if (!game.Rules.InquisitorEnabled)
        {
            return;
        }

        var seats = game.Players.OrderBy(x => x.Order).ToList();
        var holder = seats[(seats.FindIndex(x => x.PlayerId == game.LeaderPlayerId) + seats.Count - 1) % seats.Count];
        holder.WasInquisitor = true;
        game.InquisitorPlayerId = holder.PlayerId;
    }

    /// <summary>
    /// Defector module: one Defector on each side, taken from the players still without a character (there are always
    /// some, as a side has at least two spies and three members of the resistance and takes one Reverser at most), and a
    /// shuffled switch deck.
    /// </summary>
    private static void DealDefectors(ResistanceGame game, List<ResistancePlayer> shuffled, Random random)
    {
        game.DefectorDeck = string.Empty;
        if (!game.Rules.DefectorEnabled)
        {
            return;
        }

        foreach (var side in new[] { ResistanceRole.Spy, ResistanceRole.Resistance })
        {
            shuffled.First(x => x.Role == side && x.Character == ResistanceCharacter.Standard)
                .Character = ResistanceCharacter.Defector;
        }

        var cards = Enumerable.Repeat(ResistanceGame.DefectorNoChange, ResistanceGame.DefectorDeckSize - 2)
            .Concat([ResistanceGame.DefectorChangeAllegiance, ResistanceGame.DefectorChangeAllegiance]);
        game.DefectorDeck = new string(cards.Shuffled(random).ToArray());
    }

    /// <summary>
    /// Assassin module: the Assassin is the first spy and the Commander the first resistance member of the shuffle
    /// (no extra draw, so a game without the module deals as before). The module does not combine with the other
    /// characters, so every player is still a standard one here.
    /// </summary>
    private static void DealAssassin(ResistanceGame game, List<ResistancePlayer> shuffled)
    {
        game.AssassinationTargetPlayerId = null;
        if (!game.Rules.AssassinEnabled)
        {
            return;
        }

        shuffled.First(x => x.Role == ResistanceRole.Spy && x.Character == ResistanceCharacter.Standard)
            .Character = ResistanceCharacter.Assassin;
        shuffled.First(x => x.Role == ResistanceRole.Resistance && x.Character == ResistanceCharacter.Standard)
            .Character = ResistanceCharacter.Commander;

        // The optional characters take the next standard players of their side; the start already checked they fit
        var optional = new (bool Enabled, ResistanceRole Side, ResistanceCharacter Character)[]
        {
            (game.Rules.BodyGuardEnabled, ResistanceRole.Resistance, ResistanceCharacter.BodyGuard),
            (game.Rules.DeepCoverEnabled, ResistanceRole.Spy, ResistanceCharacter.DeepCover),
            (game.Rules.BlindSpyEnabled, ResistanceRole.Spy, ResistanceCharacter.BlindSpy),
            (game.Rules.FalseCommanderEnabled, ResistanceRole.Spy, ResistanceCharacter.FalseCommander),
        };
        foreach (var (enabled, side, character) in optional.Where(x => x.Enabled))
        {
            shuffled.First(x => x.Role == side && x.Character == ResistanceCharacter.Standard).Character = character;
        }
    }

    /// <summary>Gives every player a random role, picks a random first leader and starts the first mission.</summary>
    public static void Deal(ResistanceGame game, Random random)
    {
        var shuffled = game.Players.Shuffled(random);
        var spies = game.Rules.SpyCount(shuffled.Count);

        for (var i = 0; i < shuffled.Count; i++)
        {
            shuffled[i].Role = i < spies ? ResistanceRole.Spy : ResistanceRole.Resistance;
            shuffled[i].Character = ResistanceCharacter.Standard;
        }

        if (game.Rules.ReverserEnabled)
        {
            // The shuffle is random, so the first of a side is a random member of it
            var sides = game.Rules.ReverserSides;
            if (sides != ResistanceReverserSides.Resistance)
            {
                shuffled.First(x => x.Role == ResistanceRole.Spy && x.Character == ResistanceCharacter.Standard)
                    .Character = ResistanceCharacter.Reverser;
            }

            if (sides != ResistanceReverserSides.Spy)
            {
                shuffled.First(x => x.Role == ResistanceRole.Resistance && x.Character == ResistanceCharacter.Standard)
                    .Character = ResistanceCharacter.Reverser;
            }
        }

        DealDefectors(game, shuffled, random);
        DealAssassin(game, shuffled);

        game.ChangeLeader(game.Players[random.Next(game.Players.Count)].PlayerId);
        GiveInquisitorToken(game);
        BuildPlotDeck(game, random);
        ResistanceFlow.BeginMission(game);
    }
}
