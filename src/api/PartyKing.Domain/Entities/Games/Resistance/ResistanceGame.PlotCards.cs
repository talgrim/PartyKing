using PartyKing.Domain.Events.Games.Resistance;

namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>The plot cards add-on of <see cref="ResistanceGame"/>: dealing the cards, playing them and the windows around them.</summary>
public partial class ResistanceGame
{
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

        var toDeal = ResistanceRoles.CardsToDeal(this).ToDictionary(x => x.CardId);
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

        ResistanceRoles.AfterDeal(this);

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
        var card = PlotCards.Find(x => x.CardId == cardId);
        if (card is null || callerPlayerId is null || card.HolderPlayerId != callerPlayerId)
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

        if (!(spotlight || ResistanceRoles.IsImmediate(card.Type))
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.UnknownCard, this);
        }

        if (card.Location == ResistancePlotCardLocation.Played)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardState, this);
        }

        if (spotlight && ResistanceRoles.OpenMissionPlayerId(this) is not null)
        {
            // One player is forced open per mission
            return ResistanceOutcome.Failed(ResistanceFailure.CardState, this);
        }

        var validTarget = card.Type switch
        {
            ResistancePlotCardType.OverheardConversation =>
                ResistanceRoles.Neighbours(this, card.HolderPlayerId!.Value).Contains(targetPlayerId),
            ResistancePlotCardType.InTheSpotlight =>
                ResistanceRoles.CanBeForcedOpen(this, card.HolderPlayerId!.Value, targetPlayerId),
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
            || (card.HolderPlayerId != callerPlayerId && ResistanceRoles.Viewer(card) != callerPlayerId))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotYourCard, this);
        }

        if (!ResistanceRoles.IsImmediate(card.Type)
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.UnknownCard, this);
        }

        if (card.Location == ResistancePlotCardLocation.Hand || card.IsConfirmed)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardState, this);
        }

        if (ResistanceRoles.Viewer(card) != callerPlayerId)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotYourCard, this);
        }

        card.IsConfirmed = true;
        if (!ResistanceRoles.UnplayedImmediateCards(this).Any() && !ResistanceRoles.UnconfirmedCards(this).Any())
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
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        // Ownership first, so a probe of someone else's card id learns nothing about it
        var card = PlotCards.Find(x => x.CardId == cardId);
        if (card is null || callerPlayerId is null || card.HolderPlayerId != callerPlayerId)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotYourCard, this);
        }

        if (card.Type != ResistancePlotCardType.TakeResponsibility
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.UnknownCard, this);
        }

        if (card.Location == ResistancePlotCardLocation.Played)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardState, this);
        }

        if (Phase is not (ResistancePhase.SelectingTeam or ResistancePhase.PlayingMission
            or ResistancePhase.PeekWindow or ResistancePhase.MissionResult))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardNotPlayableNow, this);
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
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        // Ownership first, so a probe of someone else's card id learns nothing about it
        var card = PlotCards.Find(x => x.CardId == cardId);
        if (card is null || callerPlayerId is null || card.HolderPlayerId != callerPlayerId)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotYourCard, this);
        }

        if (card.Type != ResistancePlotCardType.StrongLeader
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.UnknownCard, this);
        }

        if (card.Location == ResistancePlotCardLocation.Played)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardState, this);
        }

        if (Phase != ResistancePhase.LeaderWindow)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardNotPlayableNow, this);
        }

        if (callerPlayerId == LeaderPlayerId)
        {
            // Taking the lead from yourself changes nothing
            return ResistanceOutcome.Failed(ResistanceFailure.InvalidTarget, this);
        }

        MarkPlayed(card, LeaderPlayerId, playedAt, confirmed: true);
        ChangeLeader(callerPlayerId);
        ResistanceRoles.AfterWindow(this);

        return Succeeded();
    }

    /// <summary>
    /// After a mission is played (until "Dalej") the holder of Keeping a Close Eye looks at one team member's mission
    /// card; a card can be looked at by one player only and never your own. The play is public, the card is not.
    /// </summary>
    public ResistanceOutcome PeekMissionCard(
        Guid? callerPlayerId, int cardId, Guid targetPlayerId, DateTimeOffset playedAt)
    {
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        // Ownership first, so a probe of someone else's card id learns nothing about it
        var card = PlotCards.Find(x => x.CardId == cardId);
        if (card is null || callerPlayerId is null || card.HolderPlayerId != callerPlayerId)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotYourCard, this);
        }

        if (card.Type != ResistancePlotCardType.KeepingCloseEye
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.UnknownCard, this);
        }

        if (card.Location == ResistancePlotCardLocation.Played)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardState, this);
        }

        if (Phase != ResistancePhase.PeekWindow)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardNotPlayableNow, this);
        }

        if (!ResistanceRoles.CanPeek(this, callerPlayerId.Value, targetPlayerId))
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
        if (Inactive() is { } inactive)
        {
            return inactive;
        }

        // Ownership first, so a probe of someone else's card id learns nothing about it
        var card = PlotCards.Find(x => x.CardId == cardId);
        if (card is null || callerPlayerId is null || card.HolderPlayerId != callerPlayerId)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.NotYourCard, this);
        }

        if (card.Type != ResistancePlotCardType.NoConfidence
            || card.Location is not (ResistancePlotCardLocation.Hand or ResistancePlotCardLocation.Played))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.UnknownCard, this);
        }

        if (card.Location == ResistancePlotCardLocation.Played)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardState, this);
        }

        if (Phase != ResistancePhase.ConfidenceWindow)
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardNotPlayableNow, this);
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

        ResistanceRoles.RejectTeam(this);

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

        if (!ResistanceRoles.IsConfirmationWindow(this))
        {
            return ResistanceOutcome.Failed(ResistanceFailure.CardNotPlayableNow, this);
        }

        player.PassedLeaderWindow = true;
        if (Players.All(x => x.PassedLeaderWindow))
        {
            ResistanceRoles.AfterWindow(this);
        }

        return Succeeded();
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
