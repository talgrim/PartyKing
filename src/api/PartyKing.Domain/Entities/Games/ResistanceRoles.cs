namespace PartyKing.Domain.Entities.Games;

/// <summary>Dealing the roles (and plot cards) of a Resistance game and moving it from one step to the next.</summary>
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
        foreach (var player in game.Players)
        {
            player.IsOnTeam = false;
            player.Vote = null;
            player.MissionCard = null;
        }

        BeginMission(game);
    }

    /// <summary>
    /// The leader of a new mission picks the team, after drawing and handing out the plot cards when the add-on is on.
    /// </summary>
    public static void BeginMission(ResistanceGame game)
    {
        game.Phase = ResistancePhase.SelectingTeam;
        if (!game.Rules.PlotCardsEnabled)
        {
            return;
        }

        var toDraw = game.Rules.PlotCardsPerRound(game.Players.Count);
        foreach (var card in game.PlotCards
                     .Where(x => x.Location == ResistancePlotCardLocation.Deck)
                     .OrderBy(x => x.DeckOrder)
                     .Take(toDraw))
        {
            card.Location = ResistancePlotCardLocation.Drawn;
            card.HolderPlayerId = game.LeaderPlayerId;
        }

        game.Phase = ResistancePhase.DealingCards;
    }

    /// <summary>The cards the leader hands out: the drawn ones except Establish Confidence, which the leader plays on themselves.</summary>
    public static IEnumerable<ResistancePlotCard> CardsToDeal(ResistanceGame game) =>
        game.PlotCards.Where(x => x.Location == ResistancePlotCardLocation.Drawn
                                  && x.Type != ResistancePlotCardType.EstablishConfidence);

    /// <summary>Whether the card is played the moment it is dealt and needs its receiver to confirm what they saw.</summary>
    public static bool IsImmediate(ResistancePlotCardType type) =>
        type is ResistancePlotCardType.OverheardConversation
            or ResistancePlotCardType.EstablishConfidence
            or ResistancePlotCardType.OpenUp;

    /// <summary>The immediate cards in a hand that have not been played yet.</summary>
    public static IEnumerable<ResistancePlotCard> UnplayedImmediateCards(ResistanceGame game) =>
        game.PlotCards.Where(x => x.Location == ResistancePlotCardLocation.Hand && IsImmediate(x.Type));

    /// <summary>The played immediate cards whose receiver has not confirmed yet; other cards show nobody an identity card.</summary>
    public static IEnumerable<ResistancePlotCard> UnconfirmedCards(ResistanceGame game) =>
        game.PlotCards.Where(x => x.Location == ResistancePlotCardLocation.Played && IsImmediate(x.Type) && !x.IsConfirmed);

    /// <summary>The player who sees the identity card an immediate card shows: the holder of Overheard Conversation, else the target.</summary>
    public static Guid? Viewer(ResistancePlotCard card) =>
        card.Type == ResistancePlotCardType.OverheardConversation ? card.HolderPlayerId : card.TargetPlayerId;

    /// <summary>The player whose identity card an immediate card shows: the target of Overheard Conversation, else the holder.</summary>
    public static Guid? Revealed(ResistancePlotCard card) =>
        card.Type == ResistancePlotCardType.OverheardConversation ? card.TargetPlayerId : card.HolderPlayerId;

    /// <summary>
    /// The team member who must play their mission card openly this mission (In the Spotlight), or null. The card
    /// stays in the log, so it only counts for the mission it was played in.
    /// </summary>
    public static Guid? OpenMissionPlayerId(ResistanceGame game) =>
        game.PlotCards
            .Where(x => x.Type == ResistancePlotCardType.InTheSpotlight
                        && x.Location == ResistancePlotCardLocation.Played
                        && x.PlayedInMission == game.MissionNumber)
            .Select(x => x.TargetPlayerId)
            .FirstOrDefault();

    /// <summary>Whether In the Spotlight may force the target open: another player of the team who has not played yet.</summary>
    public static bool CanBeForcedOpen(ResistanceGame game, Guid holderPlayerId, Guid targetPlayerId) =>
        targetPlayerId != holderPlayerId
        && game.Players.Any(x => x.PlayerId == targetPlayerId && x.IsOnTeam && x.MissionCard is null);

    /// <summary>After the cards are dealt: plays the immediate ones first, otherwise the leader picks the team.</summary>
    public static void AfterDeal(ResistanceGame game) =>
        game.Phase = UnplayedImmediateCards(game).Any() ? ResistancePhase.ResolvingCards : ResistancePhase.SelectingTeam;

    /// <summary>The players seated directly beside the player, the one before first, wrapping around the table.</summary>
    public static IReadOnlyList<Guid> Neighbours(ResistanceGame game, Guid playerId)
    {
        var seats = game.Players.OrderBy(x => x.Order).Select(x => x.PlayerId).ToList();
        var index = seats.IndexOf(playerId);
        return index < 0
            ? []
            : [seats[(index + seats.Count - 1) % seats.Count], seats[(index + 1) % seats.Count]];
    }

    /// <summary>The players with an Opinion Maker in play: they reveal their vote before the others vote.</summary>
    public static IReadOnlyList<Guid> OpinionMakerIds(ResistanceGame game) =>
        game.PlotCards
            .Where(x => x.Location == ResistancePlotCardLocation.InPlay
                        && x.Type == ResistancePlotCardType.OpinionMaker
                        && x.HolderPlayerId is not null)
            .Select(x => x.HolderPlayerId!.Value)
            .Distinct()
            .ToList();

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
        var order = Enumerable.Range(0, types.Count).OrderBy(_ => random.Next()).ToList();

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
        }
    }

    /// <summary>Gives every player a random role, picks a random first leader and starts the first mission.</summary>
    public static void Deal(ResistanceGame game, Random random)
    {
        var shuffled = game.Players.OrderBy(_ => random.Next()).ToList();
        var spies = game.Rules.SpyCount(shuffled.Count);

        for (var i = 0; i < shuffled.Count; i++)
        {
            shuffled[i].Role = i < spies ? ResistanceRole.Spy : ResistanceRole.Resistance;
        }

        game.LeaderPlayerId = game.Players[random.Next(game.Players.Count)].PlayerId;
        BuildPlotDeck(game, random);
        BeginMission(game);
    }
}
