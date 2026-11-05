using PartyKing.Domain.Entities.Games.Base;
using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>Dealing the roles (and plot cards) of a Resistance game and moving it from one step to the next.</summary>
public static class ResistanceRoles
{
    /// <summary>The Inquisitor examines right after these missions (2 to 4), three times in all.</summary>
    public const int FirstInquisitionMission = 2;

    public const int LastInquisitionMission = 4;

    /// <summary>A team is approved by a strict majority of all the players; a tie rejects it.</summary>
    public static bool IsApproved(ResistanceGame game)
    {
        return game.Players.Count(x => x.Vote == true) * 2 > game.Players.Count;
    }

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
            game.Publish(new ResistanceTeamApproved(game.SessionId, game.MissionNumber));
            if (HasPlayableCards(game))
            {
                OpenWindow(game, ResistancePhase.ConfidenceWindow, beforeDraw: false);
                return;
            }

            StartMission(game);
            return;
        }

        RejectTeam(game);
    }

    private static void StartMission(ResistanceGame game)
    {
        game.RejectedTeams = 0;
        game.Phase = ResistancePhase.PlayingMission;
    }

    /// <summary>
    /// Whether <paramref name="target"/>'s mission card of the finished mission can be peeked at (in the peek window): it was played,
    /// is not the peeker's own and the peeker has not looked at it yet (other players may look at the same card).
    /// </summary>
    public static bool CanPeek(ResistanceGame game, Guid peeker, Guid target)
    {
        return game.Phase == ResistancePhase.PeekWindow
               && target != peeker
               && game.Players.Any(x => x.PlayerId == target && x.MissionCard is not null)
               && !game.PlotCards.Any(x => x.Type == ResistancePlotCardType.KeepingCloseEye
                                           && x.Location == ResistancePlotCardLocation.Played
                                           && x.PlayedInMission == game.MissionNumber
                                           && x.HolderPlayerId == peeker
                                           && x.TargetPlayerId == target);
    }

    /// <summary>The mission cards <paramref name="peeker"/> looked at this mission (player id, card); private to them.</summary>
    public static IEnumerable<(Guid PlayerId, ResistanceMissionCardKind Card)> PeekedCards(ResistanceGame game, Guid peeker)
    {
        return !game.IsMissionResolved
            ? []
            : game.PlotCards
                .Where(x => x.Type == ResistancePlotCardType.KeepingCloseEye
                            && x.Location == ResistancePlotCardLocation.Played
                            && x.PlayedInMission == game.MissionNumber
                            && x.HolderPlayerId == peeker && x.TargetPlayerId is not null)
                .Select(x => (x.TargetPlayerId!.Value,
                    game.Players.First(p => p.PlayerId == x.TargetPlayerId).MissionCard!.Value));
    }

    /// <summary>
    /// Counts the team as rejected (a vote, or a No Confidence cancelling the approval): the fifth ends the game, else
    /// the lead passes on and the next leader picks.
    /// </summary>
    public static void RejectTeam(ResistanceGame game)
    {
        game.RejectedTeams++;
        game.Publish(new ResistanceTeamRejected(game.SessionId, game.MissionNumber, game.RejectedTeams));
        if (game.RejectedTeams >= ResistanceRules.MaxRejectedTeams)
        {
            game.Phase = ResistancePhase.GameOver;
            game.Winner = ResistanceRole.Spy;
            game.Publish(new ResistanceGameOver(game.SessionId, ResistanceRole.Spy));
            return;
        }

        // The next leader picks at once; the votes stay (and are public) until that team is submitted
        game.ChangeLeader(NextLeader(game));
        game.Phase = ResistancePhase.SelectingTeam;
        foreach (var player in game.Players)
        {
            player.IsOnTeam = false;
        }

        if (HasPlayableCards(game))
        {
            OpenWindow(game, ResistancePhase.LeaderWindow, beforeDraw: false);
        }
    }

    /// <summary>
    /// Whether anyone holds a playable plot card in their hand. The confirmation windows open exactly then, whatever
    /// the cards are, so that the window itself never tells which card is out there (the hand sizes are public anyway).
    /// A face-up Opinion Maker is not a card to play.
    /// </summary>
    public static bool HasPlayableCards(ResistanceGame game)
    {
        return game.Rules.PlotCardsEnabled
               && game.PlotCards.Any(x => x.Location == ResistancePlotCardLocation.Hand
                                          && x.Type != ResistancePlotCardType.OpinionMaker);
    }

    private static void OpenWindow(ResistanceGame game, ResistancePhase phase, bool beforeDraw)
    {
        game.Phase = phase;
        game.LeaderWindowBeforeDraw = beforeDraw;
        foreach (var player in game.Players)
        {
            player.PassedLeaderWindow = false;
        }
    }

    /// <summary>
    /// Ends a window (everyone passed, or a card was played). The leader window goes on to the draw or the team; the
    /// confidence window, passed by everyone, starts the mission, and the peek window shows the "Dalej" step.
    /// </summary>
    public static void AfterWindow(ResistanceGame game)
    {
        foreach (var player in game.Players)
        {
            player.PassedLeaderWindow = false;
        }

        if (game.Phase == ResistancePhase.ConfidenceWindow)
        {
            StartMission(game);
            return;
        }

        if (game.Phase == ResistancePhase.PeekWindow)
        {
            AfterMissionResolved(game);
            return;
        }

        if (game.LeaderWindowBeforeDraw)
        {
            DrawPlotCards(game);
            return;
        }

        game.Phase = ResistancePhase.SelectingTeam;
    }

    /// <summary>
    /// After a finished mission: the next player leads and picks again in the next mission, the team, the votes and the
    /// cards are cleared.
    /// </summary>
    public static void PassLeadership(ResistanceGame game)
    {
        game.MissionNumber++;
        game.ChangeLeader(NextLeader(game));
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
        FlipDefectorCard(game);
        if (!game.Rules.PlotCardsEnabled)
        {
            return;
        }

        if (HasPlayableCards(game))
        {
            OpenWindow(game, ResistancePhase.LeaderWindow, beforeDraw: true);
            return;
        }

        DrawPlotCards(game);
    }

    /// <summary>
    /// Defector module: from the third mission on, the card on top of the switch deck is flipped as the mission begins;
    /// Change Allegiance puts both Defectors on the other side, so the win checks and the mission card rules follow
    /// from <see cref="ResistancePlayer.Role"/> at once. A rejected team does not begin a mission, so it flips nothing.
    /// </summary>
    private static void FlipDefectorCard(ResistanceGame game)
    {
        var index = game.MissionNumber - ResistanceGame.DefectorFirstFlipMission;
        if (index < 0 || index >= game.DefectorDeck.Length || game.DefectorDeck[index] != ResistanceGame.DefectorChangeAllegiance)
        {
            return;
        }

        foreach (var defector in game.Players.Where(x => x.Character == ResistanceCharacter.Defector))
        {
            defector.SwitchAllegiance();
        }
    }

    private static void DrawPlotCards(ResistanceGame game)
    {
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

    /// <summary>Whether the leader still has to hand the card out: it is drawn and is not Establish Confidence, which the leader plays on themselves.</summary>
    public static bool NeedsDealing(ResistancePlotCard card)
    {
        return card.Location == ResistancePlotCardLocation.Drawn &&
               card.Type != ResistancePlotCardType.EstablishConfidence;
    }

    /// <summary>The cards the leader hands out (see <see cref="NeedsDealing"/>).</summary>
    public static IEnumerable<ResistancePlotCard> CardsToDeal(ResistanceGame game)
    {
        return game.PlotCards.Where(NeedsDealing);
    }

    /// <summary>
    /// A window in which every player confirms (or the holder of a card plays it) before the game goes on: the leader
    /// window, the confidence window and the peek window.
    /// </summary>
    public static bool IsConfirmationWindow(ResistanceGame game)
    {
        return game.Phase is ResistancePhase.LeaderWindow or ResistancePhase.ConfidenceWindow
            or ResistancePhase.PeekWindow;
    }

    /// <summary>
    /// Whether everybody's vote is public: all have voted and the vote is no longer on (also while the next leader
    /// picks after a rejection). While it is on, only who voted is known, not how.
    /// </summary>
    public static bool VotesRevealed(ResistanceGame game)
    {
        return game.Phase != ResistancePhase.Voting && game.Players.All(x => x.Vote is not null);
    }

    /// <summary>
    /// Whether the votes of the Opinion Makers are public: there is one in play and every one of them has voted, which
    /// happens before the others may vote.
    /// </summary>
    public static bool OpinionMakerVotesRevealed(ResistanceGame game)
    {
        var opinionMakers = OpinionMakerIds(game);

        return opinionMakers.Count > 0
               && game.Players.Where(x => opinionMakers.Contains(x.PlayerId)).All(x => x.Vote is not null);
    }

    /// <summary>Whether the card is played the moment it is dealt and needs its receiver to confirm what they saw.</summary>
    public static bool IsImmediate(ResistancePlotCardType type)
    {
        return type is ResistancePlotCardType.OverheardConversation
            or ResistancePlotCardType.EstablishConfidence
            or ResistancePlotCardType.OpenUp;
    }

    /// <summary>The immediate cards in a hand that have not been played yet.</summary>
    public static IEnumerable<ResistancePlotCard> UnplayedImmediateCards(ResistanceGame game)
    {
        return game.PlotCards.Where(x => x.Location == ResistancePlotCardLocation.Hand && IsImmediate(x.Type));
    }

    /// <summary>The played immediate cards whose receiver has not confirmed yet; other cards show nobody an identity card.</summary>
    public static IEnumerable<ResistancePlotCard> UnconfirmedCards(ResistanceGame game)
    {
        return game.PlotCards.Where(x =>
            x.Location == ResistancePlotCardLocation.Played && IsImmediate(x.Type) && !x.IsConfirmed);
    }

    /// <summary>The player who sees the identity card an immediate card shows: the holder of Overheard Conversation, else the target.</summary>
    public static Guid? Viewer(ResistancePlotCard card)
    {
        return card.Type == ResistancePlotCardType.OverheardConversation ? card.HolderPlayerId : card.TargetPlayerId;
    }

    /// <summary>The player whose identity card an immediate card shows: the target of Overheard Conversation, else the holder.</summary>
    public static Guid? Revealed(ResistancePlotCard card)
    {
        return card.Type == ResistancePlotCardType.OverheardConversation ? card.TargetPlayerId : card.HolderPlayerId;
    }

    /// <summary>
    /// The team member who must play their mission card openly this mission (In the Spotlight), or null. The card
    /// stays in the log, so it only counts for the mission it was played in.
    /// </summary>
    public static Guid? OpenMissionPlayerId(ResistanceGame game)
    {
        return game.PlotCards
            .Where(x => x.Type == ResistancePlotCardType.InTheSpotlight
                        && x.Location == ResistancePlotCardLocation.Played
                        && x.PlayedInMission == game.MissionNumber)
            .Select(x => x.TargetPlayerId)
            .FirstOrDefault();
    }

    /// <summary>Whether In the Spotlight may force the target open: another player of the team who has not played yet.</summary>
    public static bool CanBeForcedOpen(ResistanceGame game, Guid holderPlayerId, Guid targetPlayerId)
    {
        return targetPlayerId != holderPlayerId
               && game.Players.Any(x => x.PlayerId == targetPlayerId && x.IsOnTeam && x.MissionCard is null);
    }

    /// <summary>After the cards are dealt: plays the immediate ones first, otherwise the leader picks the team.</summary>
    public static void AfterDeal(ResistanceGame game)
    {
        game.Phase = UnplayedImmediateCards(game).Any()
            ? ResistancePhase.ResolvingCards
            : ResistancePhase.SelectingTeam;
    }

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
    public static IReadOnlyList<Guid> OpinionMakerIds(ResistanceGame game)
    {
        return game.PlotCards
            .Where(x => x.Location == ResistancePlotCardLocation.InPlay
                        && x.Type == ResistancePlotCardType.OpinionMaker
                        && x.HolderPlayerId is not null)
            .Select(x => x.HolderPlayerId!.Value)
            .Distinct()
            .ToList();
    }

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

    /// <summary>Whether every member of the approved team has played a card.</summary>
    public static bool AllCardsPlayed(ResistanceGame game)
    {
        return game.Players.Where(x => x.IsOnTeam).All(x => x.MissionCard is not null);
    }

    /// <summary>
    /// Applies the complete set of cards (see <see cref="ResistanceMissionRules"/>): the outcome and the number of
    /// Fail and Reverse cards are recorded (the played cards are shown to everybody without who played them, as when
    /// the cards are revealed shuffled at the table), and the third success or failure ends the game.
    /// </summary>
    public static void ResolveMission(ResistanceGame game)
    {
        var cards = game.Players.Where(x => x.IsOnTeam).Select(x => x.MissionCard!.Value).ToList();
        var fails = cards.Count(x => x == ResistanceMissionCardKind.Fail);
        var reverses = cards.Count(x => x == ResistanceMissionCardKind.Reverse);
        var succeeded = ResistanceMissionRules.HasSucceeded(cards, game.Rules.RequiredFails(game.Players.Count, game.MissionNumber));

        game.MissionResults.Add(new ResistanceMissionResult
        {
            SessionId = game.SessionId,
            MissionNumber = game.MissionNumber,
            FailCount = fails,
            ReverseCount = reverses,
            Succeeded = succeeded,
            TeamPlayerIds = game.Players.Where(x => x.IsOnTeam).OrderBy(x => x.Order).Select(x => x.PlayerId).ToList(),
        });

        game.Publish(new ResistanceMissionCompleted(game.SessionId, game.MissionNumber, succeeded, fails, reverses));

        // Assassin module: the third success is not the end yet, the Assassin still names the Commander. Nothing else
        // happens in between (no peek window, no Inquisition), as after any mission that decides the game.
        if (game.Rules.AssassinEnabled && game.ResistanceScore >= ResistanceRules.MissionsToWin)
        {
            game.Phase = ResistancePhase.Assassination;
            return;
        }

        if (game.ResistanceScore >= ResistanceRules.MissionsToWin || game.SpyScore >= ResistanceRules.MissionsToWin)
        {
            game.Phase = ResistancePhase.GameOver;
            game.Winner = game.ResistanceScore >= ResistanceRules.MissionsToWin ? ResistanceRole.Resistance : ResistanceRole.Spy;
            game.Publish(new ResistanceGameOver(game.SessionId, game.Winner.Value));
            return;
        }

        // The outcome is public at once; a playable card in a hand opens the window for Keeping a Close Eye first
        if (HasPlayableCards(game))
        {
            OpenWindow(game, ResistancePhase.PeekWindow, beforeDraw: false);
            return;
        }

        AfterMissionResolved(game);
    }

    /// <summary>
    /// The outcome of a mission that did not end the game is public and its windows are over: the Inquisitor, when the
    /// module is on, examines after missions 2, 3 and 4; then the "Dalej" step.
    /// </summary>
    private static void AfterMissionResolved(ResistanceGame game)
    {
        game.Phase = game.Rules.InquisitorEnabled && game.InquisitorPlayerId is not null
                                                  && game.MissionNumber is >= FirstInquisitionMission and <= LastInquisitionMission
            ? ResistancePhase.Inquisition
            : ResistancePhase.MissionResult;
    }

    /// <summary>
    /// Whether <paramref name="target"/> may be examined by <paramref name="examiner"/>: another player who never held
    /// the Inquisitor token.
    /// </summary>
    public static bool CanExamine(ResistanceGame game, Guid examiner, Guid target)
    {
        return target != examiner && game.Players.Any(x => x.PlayerId == target && !x.WasInquisitor);
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
        BeginMission(game);
    }
}
