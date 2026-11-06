using PartyKing.Domain.Entities.Games.Resistance.PlotCards;

namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>What the plot cards in a Resistance game allow and show: who may play or see what, and which cards are in play.</summary>
public static class ResistancePlotCardQueries
{
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
    /// Whether anyone holds a playable plot card in their hand. The confirmation windows open exactly then, whatever
    /// the cards are, so that the window itself never tells which card is out there (the hand sizes are public anyway).
    /// A face-up Opinion Maker is not in a hand, so it never counts.
    /// </summary>
    public static bool HasPlayableCards(ResistanceGame game)
    {
        return game.Rules.PlotCardsEnabled
               && game.PlotCards.Any(x => x.Location == ResistancePlotCardLocation.Hand);
    }

    /// <summary>Whether the leader still has to hand the card out: it is drawn and is not Establish Confidence, which the leader plays on themselves.</summary>
    public static bool NeedsDealing(ResistancePlotCard card)
    {
        return card.Location == ResistancePlotCardLocation.Drawn &&
               ResistancePlotCardBehaviors.For(card.Type).IsDealtByLeader;
    }

    /// <summary>The cards the leader hands out (see <see cref="NeedsDealing"/>).</summary>
    public static IEnumerable<ResistancePlotCard> CardsToDeal(ResistanceGame game)
    {
        return game.PlotCards.Where(NeedsDealing);
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
        return ResistancePlotCardBehaviors.For(type) is IImmediateBehavior;
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
        return ResistancePlotCardBehaviors.For(card.Type) is IImmediateBehavior immediate
            ? immediate.Viewer(card)
            : card.TargetPlayerId;
    }

    /// <summary>The player whose identity card an immediate card shows: the target of Overheard Conversation, else the holder.</summary>
    public static Guid? Revealed(ResistancePlotCard card)
    {
        return ResistancePlotCardBehaviors.For(card.Type) is IImmediateBehavior immediate
            ? immediate.Revealed(card)
            : card.HolderPlayerId;
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
}
