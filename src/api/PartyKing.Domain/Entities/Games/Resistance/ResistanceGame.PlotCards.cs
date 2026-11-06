using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>The plot cards add-on of <see cref="ResistanceGame"/>: dealing the cards, playing them and the windows around them.</summary>
public partial class ResistanceGame
{
    /// <summary>The plot cards of the add-on (deck, drawn and in hands); empty when the add-on is off.</summary>
    public List<ResistancePlotCard> PlotCards { get; internal set; } = [];

    /// <summary>Switches the plot cards add-on on or off in the rules. Allowed until the game starts, open or not.</summary>
    public ResistanceOutcome SetPlotCards(bool enabled)
    {
        if (IsStarted)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.PlotCardsLocked, this);
        }

        // Rules is a JSON column compared by reference, so assign a copy to have the change detected.
        Rules = Rules.Clone(enabled);

        return Succeeded();
    }

    /// <summary>
    /// The leader hands the cards drawn for this mission to the other players (every card but Establish Confidence,
    /// which the leader keeps), which moves the game on to picking the team.
    /// </summary>
    public ResistanceOutcome DealPlotCards(Guid? callerPlayerId, IReadOnlyList<PlotCardAssignment> assignments)
    {
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        if (callerPlayerId is null || callerPlayerId != LeaderPlayerId)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotLeaderToDeal, this);
        }

        if (Phase != ResistancePhase.DealingCards)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotDealing, this);
        }

        var toDeal = ResistancePlotCardQueries.CardsToDeal(this).ToDictionary(x => x.CardId);
        if (assignments.Any(x => !toDeal.ContainsKey(x.CardId))
            || assignments.Select(x => x.CardId).Distinct().Count() != assignments.Count)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.UnknownDealCard, this);
        }

        if (assignments.Any(x => x.PlayerId == LeaderPlayerId || Players.All(p => p.PlayerId != x.PlayerId)))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.InvalidReceiver, this);
        }

        if (assignments.Count != toDeal.Count)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.IncompleteDeal, this);
        }

        foreach (var assignment in assignments)
        {
            var card = toDeal[assignment.CardId];
            // An Opinion Maker is played the moment it is dealt and stays face up
            card.Location = card.Type == ResistancePlotCardType.OpinionMaker
                ? ResistancePlotCardLocation.InPlay
                : ResistancePlotCardLocation.Hand;
            card.HolderPlayerId = assignment.PlayerId;
        }

        // The leader keeps the cards they play on themselves (Establish Confidence)
        foreach (var kept in PlotCards.Where(x => x.Location == ResistancePlotCardLocation.Drawn))
        {
            kept.Location = ResistancePlotCardLocation.Hand;
        }

        ResistanceFlow.AfterDeal(this);

        return Succeeded();
    }

    /// <summary>
    /// The holder of a card plays it on a player. An immediate card (Overheard Conversation, Establish Confidence, Open
    /// Up) is played while the immediate cards are resolved, on a neighbour for Overheard Conversation and any other
    /// player for the rest. In the Spotlight is played while the mission is played, on a team member who has not
    /// played their mission card yet, and makes that card public.
    /// </summary>
    public ResistanceOutcome PlayPlotCard(
        Guid? callerPlayerId, int cardId, Guid targetPlayerId, DateTimeOffset playedAt)
    {
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        // Ownership first, so neither the error nor the phase check tells the type of someone else's card
        var card = OwnedCard(callerPlayerId, cardId);
        if (card is null)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotYourCard, this);
        }

        var spotlight = card.Type == ResistancePlotCardType.InTheSpotlight;
        if (spotlight && Phase != ResistancePhase.ConfidenceWindow)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotPlayingMission, this);
        }

        if (!spotlight && Phase != ResistancePhase.ResolvingCards)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotResolving, this);
        }

        if (!(spotlight || ResistancePlotCardQueries.IsImmediate(card.Type))
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.UnknownCard, this);
        }

        if (card.Location == ResistancePlotCardLocation.Played)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardState, this);
        }

        if (spotlight && ResistancePlotCardQueries.OpenMissionPlayerId(this) is not null)
        {
            // One player is forced open per mission
            return ResistanceOutcome.Failed(ResistanceFailure.CardState, this);
        }

        var validTarget = card.Type switch
        {
            ResistancePlotCardType.OverheardConversation =>
                ResistancePlotCardQueries.Neighbours(this, card.HolderPlayerId!.Value).Contains(targetPlayerId),
            ResistancePlotCardType.InTheSpotlight =>
                ResistancePlotCardQueries.CanBeForcedOpen(this, card.HolderPlayerId!.Value, targetPlayerId),
            _ => targetPlayerId != card.HolderPlayerId && Players.Any(x => x.PlayerId == targetPlayerId),
        };
        if (!validTarget)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.InvalidTarget, this);
        }

        MarkPlayed(card, targetPlayerId, playedAt, confirmed: spotlight);

        return Succeeded();
    }

    /// <summary>
    /// The player who saw the identity card confirms it. The last confirmation of the round moves on to picking the team.
    /// </summary>
    public ResistanceOutcome ConfirmPlotCard(Guid? callerPlayerId, int cardId)
    {
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        if (Phase != ResistancePhase.ResolvingCards)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotResolving, this);
        }

        // The holder and the viewer are the only ones who may ask about a card; anyone else learns nothing about it
        var card = PlotCards.Find(x => x.CardId == cardId);
        if (card is null || callerPlayerId is null
            || (card.HolderPlayerId != callerPlayerId && ResistancePlotCardQueries.Viewer(card) != callerPlayerId))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotYourCard, this);
        }

        if (!ResistancePlotCardQueries.IsImmediate(card.Type)
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.UnknownCard, this);
        }

        if (card.Location == ResistancePlotCardLocation.Hand || card.IsConfirmed)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardState, this);
        }

        if (ResistancePlotCardQueries.Viewer(card) != callerPlayerId)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotYourCard, this);
        }

        card.IsConfirmed = true;
        if (!ResistancePlotCardQueries.UnplayedImmediateCards(this).Any() && !ResistancePlotCardQueries.UnconfirmedCards(this).Any())
        {
            Phase = ResistancePhase.SelectingTeam;
        }

        return Succeeded();
    }

    /// <summary>
    /// The holder of Take Responsibility takes a plot card from another player, between the votes (not while a team is
    /// voted on, which would change who votes first). With a face-up Opinion Maker and cards in hand the caller says
    /// which to take (<paramref name="takeOpinionMaker"/>); a hand card is picked at random. Nothing to take is lost.
    /// </summary>
    public ResistanceOutcome TakeResponsibility(
        Guid? callerPlayerId, int cardId, Guid targetPlayerId, bool? takeOpinionMaker, DateTimeOffset playedAt,
        Random random)
    {
        if (TryGetPlayableCard(
                callerPlayerId,
                cardId,
                ResistancePlotCardType.TakeResponsibility,
                [
                    ResistancePhase.SelectingTeam, ResistancePhase.PlayingMission, ResistancePhase.PeekWindow,
                    ResistancePhase.MissionResult,
                ],
                out var card) is { } refusal)
        {
            return refusal;
        }

        if (targetPlayerId == callerPlayerId || Players.All(x => x.PlayerId != targetPlayerId))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.InvalidTarget, this);
        }

        var opinionMaker = PlotCards.Find(x => x.HolderPlayerId == targetPlayerId
                                               && x.Location == ResistancePlotCardLocation.InPlay
                                               && x.Type == ResistancePlotCardType.OpinionMaker);
        var hand = PlotCards
            .Where(x => x.HolderPlayerId == targetPlayerId && x.Location == ResistancePlotCardLocation.Hand)
            .ToList();
        if (opinionMaker is not null && hand.Count > 0 && takeOpinionMaker is null)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.ChoiceRequired, this);
        }

        var taken = opinionMaker is not null && (hand.Count == 0 || takeOpinionMaker == true)
            ? opinionMaker
            : hand.Count > 0 ? hand[random.Next(hand.Count)] : null;
        if (taken is not null)
        {
            taken.HolderPlayerId = callerPlayerId;
        }

        MarkPlayed(card, targetPlayerId, playedAt, confirmed: true);

        return Succeeded();
    }

    /// <summary>
    /// In the leader window the holder of Strong Leader takes the lead, which ends the window: the new leader draws
    /// their cards or picks the team.
    /// </summary>
    public ResistanceOutcome TakeLead(Guid? callerPlayerId, int cardId, DateTimeOffset playedAt)
    {
        if (TryGetPlayableCard(
                callerPlayerId, cardId, ResistancePlotCardType.StrongLeader, [ResistancePhase.LeaderWindow], out var card)
            is { } refusal)
        {
            return refusal;
        }

        if (callerPlayerId == LeaderPlayerId)
        {
            // Taking the lead from yourself changes nothing
            return ResistanceOutcome.Failed(ResistanceFailure.InvalidTarget, this);
        }

        MarkPlayed(card, LeaderPlayerId, playedAt, confirmed: true);
        ChangeLeader(callerPlayerId);
        ResistanceFlow.AfterWindow(this);

        return Succeeded();
    }

    /// <summary>
    /// After a mission is played (until "Dalej") the holder of Keeping a Close Eye looks at one team member's mission
    /// card; never your own and never one you already looked at (several players may look at the same card). The play is public, the card is not.
    /// </summary>
    public ResistanceOutcome PeekMissionCard(
        Guid? callerPlayerId, int cardId, Guid targetPlayerId, DateTimeOffset playedAt)
    {
        if (TryGetPlayableCard(
                callerPlayerId, cardId, ResistancePlotCardType.KeepingCloseEye, [ResistancePhase.PeekWindow], out var card)
            is { } refusal)
        {
            return refusal;
        }

        if (!ResistancePlotCardQueries.CanPeek(this, card.HolderPlayerId!.Value, targetPlayerId))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.InvalidTarget, this);
        }

        MarkPlayed(card, targetPlayerId, playedAt, confirmed: true);

        return Succeeded();
    }

    /// <summary>
    /// In the confidence window the holder of No Confidence cancels the approved team: it counts as rejected (the fifth
    /// ends the game) and the lead passes on.
    /// </summary>
    public ResistanceOutcome PlayNoConfidence(Guid? callerPlayerId, int cardId, DateTimeOffset playedAt)
    {
        if (TryGetPlayableCard(
                callerPlayerId, cardId, ResistancePlotCardType.NoConfidence, [ResistancePhase.ConfidenceWindow], out var card)
            is { } refusal)
        {
            return refusal;
        }

        MarkPlayed(card, LeaderPlayerId, playedAt, confirmed: true);
        foreach (var player in Players)
        {
            player.PassedLeaderWindow = false;
        }

        // A rejected team never plays, so an In the Spotlight played in this window goes back to its holder's hand
        foreach (var spotlight in PlotCards.Where(x => x.Type == ResistancePlotCardType.InTheSpotlight
                                                       && x.Location == ResistancePlotCardLocation.Played
                                                       && x.PlayedInMission == MissionNumber))
        {
            spotlight.Location = ResistancePlotCardLocation.Hand;
            spotlight.TargetPlayerId = null;
            spotlight.PlayedAt = null;
            spotlight.PlayedInMission = 0;
            spotlight.IsConfirmed = false;
        }

        ResistanceFlow.RejectTeam(this);

        return Succeeded();
    }

    /// <summary>
    /// A player confirms in the leader or confidence window that they play no card (repeating it is fine). The last
    /// confirmation ends the window.
    /// </summary>
    public ResistanceOutcome PassLeaderWindow(Guid? callerPlayerId)
    {
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        var player = Players.Find(x => x.PlayerId == callerPlayerId);
        if (player is null)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotPlayerToConfirm, this);
        }

        if (!ResistanceFlow.IsConfirmationWindow(this))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardNotPlayableNow, this);
        }

        player.PassedLeaderWindow = true;
        if (Players.All(x => x.PassedLeaderWindow))
        {
            ResistanceFlow.AfterWindow(this);
        }

        return Succeeded();
    }

    /// <summary>The card of the caller, or null when there is no such card or it is someone else's (the two look the same).</summary>
    private ResistancePlotCard? OwnedCard(Guid? callerPlayerId, int cardId)
    {
        var card = PlotCards.Find(x => x.CardId == cardId);

        return card is null || callerPlayerId is null || card.HolderPlayerId != callerPlayerId ? null : card;
    }

    /// <summary>
    /// The shared guard of the cards that have one step of their own. It refuses, in this order: a game that is not
    /// running, a card that is not the caller's (first, so a probe of someone else's card id learns nothing about its
    /// type, place or phase), a card of another type or not in a hand or played, a card already played, and a phase
    /// that is not one of <paramref name="phases"/>. The target and what the card does are left to the caller. Returns
    /// the refusal, or null with <paramref name="card"/> set.
    /// </summary>
    private ResistanceOutcome? TryGetPlayableCard(
        Guid? callerPlayerId,
        int cardId,
        ResistancePlotCardType type,
        IReadOnlyCollection<ResistancePhase> phases,
        out ResistancePlotCard card)
    {
        card = null!;
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        var owned = OwnedCard(callerPlayerId, cardId);
        if (owned is null)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotYourCard, this);
        }

        if (owned.Type != type
            || owned.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.UnknownCard, this);
        }

        if (owned.Location == ResistancePlotCardLocation.Played)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardState, this);
        }

        if (!phases.Contains(Phase))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardNotPlayableNow, this);
        }

        card = owned;

        return null;
    }

    private void MarkPlayed(ResistancePlotCard card, Guid? targetPlayerId, DateTimeOffset playedAt, bool confirmed)
    {
        card.Location = ResistancePlotCardLocation.Played;
        card.TargetPlayerId = targetPlayerId;
        card.PlayedAt = playedAt;
        card.PlayedInMission = MissionNumber;
        card.IsConfirmed = confirmed;
        Raise(new ResistancePlotCardPlayed(SessionId, card.CardId, card.Type, card.HolderPlayerId, targetPlayerId));
    }
}
