using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Application.Tests;

/// <summary>The plot cards of <see cref="ResistanceGame"/>, with no database, a fixed clock and a seeded random.</summary>
public class ResistanceGamePlotCardTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    private readonly ResistanceGame _game = NewGame();

    private ResistancePlayer Leader => _game.Players[0];

    private static ResistanceGame NewGame()
    {
        var game = new ResistanceGame
        {
            SessionId = Guid.NewGuid(),
            IsOpen = true,
            StartedAt = Now,
            Phase = ResistancePhase.SelectingTeam,
        };
        for (var i = 0; i < 5; i++)
        {
            game.Players.Add(new ResistancePlayer { PlayerId = Guid.NewGuid(), Order = i, Role = ResistanceRole.Resistance });
        }

        game.LeaderPlayerId = game.Players[0].PlayerId;

        return game;
    }

    private ResistancePlotCard AddCard(
        ResistancePlotCardType type, ResistancePlotCardLocation location, ResistancePlayer? holder)
    {
        var card = new ResistancePlotCard
        {
            SessionId = _game.SessionId,
            CardId = _game.PlotCards.Count,
            Type = type,
            Location = location,
            HolderPlayerId = holder?.PlayerId,
        };
        _game.PlotCards.Add(card);

        return card;
    }

    [Fact]
    public void Dealing_HandsTheCardsOutAndKeepsEstablishConfidenceWithTheLeader()
    {
        _game.Phase = ResistancePhase.DealingCards;
        var noConfidence = AddCard(ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.Drawn, Leader);
        var opinionMaker = AddCard(ResistancePlotCardType.OpinionMaker, ResistancePlotCardLocation.Drawn, Leader);
        var confidence = AddCard(ResistancePlotCardType.EstablishConfidence, ResistancePlotCardLocation.Drawn, Leader);
        var receiver = _game.Players[1].PlayerId;

        var outcome = _game.DealPlotCards(
            Leader.PlayerId,
            [new PlotCardAssignment(noConfidence.CardId, receiver), new PlotCardAssignment(opinionMaker.CardId, receiver)]);

        Assert.Null(outcome.Failure);
        Assert.Equal(ResistancePlotCardLocation.Hand, noConfidence.Location);
        Assert.Equal(receiver, noConfidence.HolderPlayerId);
        Assert.Equal(ResistancePlotCardLocation.InPlay, opinionMaker.Location);
        Assert.Equal(ResistancePlotCardLocation.Hand, confidence.Location);
        Assert.Equal(Leader.PlayerId, confidence.HolderPlayerId);
        Assert.Equal(ResistancePhase.ResolvingCards, _game.Phase);
    }

    [Fact]
    public void Dealing_IsCheckedInOrderAndChangesNothingWhenRefused()
    {
        _game.Phase = ResistancePhase.DealingCards;
        var card = AddCard(ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.Drawn, Leader);
        var other = _game.Players[1].PlayerId;

        Assert.Equal(ResistanceFailure.NotLeaderToDeal, _game.DealPlotCards(other, []).Failure);
        Assert.Equal(
            ResistanceFailure.UnknownDealCard, _game.DealPlotCards(Leader.PlayerId, [new(99, other)]).Failure);
        Assert.Equal(
            ResistanceFailure.UnknownDealCard,
            _game.DealPlotCards(Leader.PlayerId, [new(card.CardId, other), new(card.CardId, other)]).Failure);
        Assert.Equal(
            ResistanceFailure.InvalidReceiver, _game.DealPlotCards(Leader.PlayerId, [new(card.CardId, Leader.PlayerId)]).Failure);
        Assert.Equal(
            ResistanceFailure.InvalidReceiver, _game.DealPlotCards(Leader.PlayerId, [new(card.CardId, Guid.NewGuid())]).Failure);
        Assert.Equal(ResistanceFailure.IncompleteDeal, _game.DealPlotCards(Leader.PlayerId, []).Failure);
        Assert.Equal(ResistancePlotCardLocation.Drawn, card.Location);
        Assert.Equal(ResistancePhase.DealingCards, _game.Phase);

        _game.Phase = ResistancePhase.SelectingTeam;
        Assert.Equal(ResistanceFailure.NotDealing, _game.DealPlotCards(Leader.PlayerId, []).Failure);
    }

    [Fact]
    public void EveryCardAction_IsRefusedBeforeTheStartAndAfterTheEnd()
    {
        var card = AddCard(ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.Hand, Leader);
        var id = Leader.PlayerId;
        var other = _game.Players[1].PlayerId;

        ResistanceOutcome[] Everything() =>
        [
            _game.DealPlotCards(id, []),
            _game.PlayPlotCard(id, card.CardId, other, Now),
            _game.ConfirmPlotCard(id, card.CardId),
            _game.TakeResponsibility(id, card.CardId, other, null, Now, new Random(1)),
            _game.TakeLead(id, card.CardId, Now),
            _game.PeekMissionCard(id, card.CardId, other, Now),
            _game.PlayNoConfidence(id, card.CardId, Now),
            _game.PassLeaderWindow(id),
        ];

        _game.StartedAt = null;
        Assert.All(Everything(), x => Assert.Equal(ResistanceFailure.NotStarted, x.Failure));

        _game.StartedAt = Now;
        _game.Phase = ResistancePhase.GameOver;
        Assert.All(Everything(), x => Assert.Equal(ResistanceFailure.GameOver, x.Failure));
    }

    [Fact]
    public void AnImmediateCard_IsPlayedOnANeighbourAndConfirmedByItsViewer()
    {
        _game.Phase = ResistancePhase.ResolvingCards;
        var holder = _game.Players[2];
        var card = AddCard(ResistancePlotCardType.OverheardConversation, ResistancePlotCardLocation.Hand, holder);
        var neighbour = _game.Players[3].PlayerId;

        Assert.Equal(
            ResistanceFailure.InvalidTarget, _game.PlayPlotCard(holder.PlayerId, card.CardId, _game.Players[4].PlayerId, Now).Failure);
        Assert.Equal(ResistanceFailure.NotYourCard, _game.PlayPlotCard(Leader.PlayerId, card.CardId, neighbour, Now).Failure);
        Assert.Equal(ResistanceFailure.CardState, _game.ConfirmPlotCard(holder.PlayerId, card.CardId).Failure);

        Assert.Null(_game.PlayPlotCard(holder.PlayerId, card.CardId, neighbour, Now).Failure);
        Assert.Equal(ResistancePlotCardLocation.Played, card.Location);
        Assert.Equal(neighbour, card.TargetPlayerId);
        Assert.Equal(Now, card.PlayedAt);
        Assert.Equal(_game.MissionNumber, card.PlayedInMission);
        Assert.Equal(ResistanceFailure.CardState, _game.PlayPlotCard(holder.PlayerId, card.CardId, neighbour, Now).Failure);

        Assert.Equal(ResistanceFailure.NotYourCard, _game.ConfirmPlotCard(neighbour, card.CardId).Failure);
        Assert.Null(_game.ConfirmPlotCard(holder.PlayerId, card.CardId).Failure);
        Assert.Equal(ResistancePhase.SelectingTeam, _game.Phase);
        Assert.Equal(ResistanceFailure.NotResolving, _game.ConfirmPlotCard(holder.PlayerId, card.CardId).Failure);
    }

    [Fact]
    public void ACardOutsideItsStep_IsRefused()
    {
        var spotlight = AddCard(ResistancePlotCardType.InTheSpotlight, ResistancePlotCardLocation.Hand, Leader);
        var immediate = AddCard(ResistancePlotCardType.OpenUp, ResistancePlotCardLocation.Hand, Leader);
        var target = _game.Players[1].PlayerId;

        Assert.Equal(ResistanceFailure.NotPlayingMission, _game.PlayPlotCard(Leader.PlayerId, spotlight.CardId, target, Now).Failure);
        Assert.Equal(ResistanceFailure.NotResolving, _game.PlayPlotCard(Leader.PlayerId, immediate.CardId, target, Now).Failure);

        _game.Phase = ResistancePhase.ResolvingCards;
        Assert.Equal(ResistanceFailure.NotYourCard, _game.PlayPlotCard(Leader.PlayerId, 99, target, Now).Failure);
        Assert.Equal(ResistanceFailure.NotPlayingMission, _game.PlayPlotCard(Leader.PlayerId, spotlight.CardId, target, Now).Failure);

        var notImmediate = AddCard(ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.Hand, Leader);
        Assert.Equal(ResistanceFailure.UnknownCard, _game.PlayPlotCard(Leader.PlayerId, notImmediate.CardId, target, Now).Failure);
    }

    [Fact]
    public void TakeResponsibility_TakesTheOpinionMakerOrAHandCardAsTheCallerChooses()
    {
        var holder = _game.Players[1];
        var target = _game.Players[2];
        var card = AddCard(ResistancePlotCardType.TakeResponsibility, ResistancePlotCardLocation.Hand, holder);
        var opinionMaker = AddCard(ResistancePlotCardType.OpinionMaker, ResistancePlotCardLocation.InPlay, target);
        var inHand = AddCard(ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.Hand, target);

        var unanswered = _game.TakeResponsibility(holder.PlayerId, card.CardId, target.PlayerId, null, Now, new Random(1));
        Assert.Equal(ResistanceFailure.ChoiceRequired, unanswered.Failure);
        Assert.Equal(ResistancePlotCardLocation.Hand, card.Location);

        var outcome = _game.TakeResponsibility(holder.PlayerId, card.CardId, target.PlayerId, true, Now, new Random(1));

        Assert.Null(outcome.Failure);
        Assert.Equal(holder.PlayerId, opinionMaker.HolderPlayerId);
        Assert.Equal(target.PlayerId, inHand.HolderPlayerId);
        Assert.Equal(ResistancePlotCardLocation.Played, card.Location);
        Assert.True(card.IsConfirmed);
        Assert.Equal(target.PlayerId, card.TargetPlayerId);
    }

    [Fact]
    public void TakeResponsibility_PicksTheHandCardWithTheGivenRandom()
    {
        var holder = _game.Players[1];
        var target = _game.Players[2];
        AddCard(ResistancePlotCardType.TakeResponsibility, ResistancePlotCardLocation.Hand, holder);
        var first = AddCard(ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.Hand, target);
        var second = AddCard(ResistancePlotCardType.StrongLeader, ResistancePlotCardLocation.Hand, target);
        var random = new Random(7);
        var expected = new Random(7).Next(2) == 0 ? first : second;

        _game.TakeResponsibility(holder.PlayerId, 0, target.PlayerId, null, Now, random);

        Assert.Equal(holder.PlayerId, expected.HolderPlayerId);
        Assert.Equal(target.PlayerId, (expected == first ? second : first).HolderPlayerId);
    }

    [Fact]
    public void TakeResponsibility_IsRefusedMidVoteAndForBadTargets()
    {
        var holder = _game.Players[1];
        var card = AddCard(ResistancePlotCardType.TakeResponsibility, ResistancePlotCardLocation.Hand, holder);
        var target = _game.Players[2].PlayerId;

        _game.Phase = ResistancePhase.Voting;
        Assert.Equal(
            ResistanceFailure.CardNotPlayableNow,
            _game.TakeResponsibility(holder.PlayerId, card.CardId, target, null, Now, new Random(1)).Failure);

        _game.Phase = ResistancePhase.SelectingTeam;
        Assert.Equal(
            ResistanceFailure.InvalidTarget,
            _game.TakeResponsibility(holder.PlayerId, card.CardId, holder.PlayerId, null, Now, new Random(1)).Failure);
        Assert.Equal(
            ResistanceFailure.InvalidTarget,
            _game.TakeResponsibility(holder.PlayerId, card.CardId, Guid.NewGuid(), null, Now, new Random(1)).Failure);
    }

    [Fact]
    public void TakeLead_InTheLeaderWindow_MovesTheLeadAndEndsTheWindow()
    {
        _game.Phase = ResistancePhase.LeaderWindow;
        var holder = _game.Players[3];
        var card = AddCard(ResistancePlotCardType.StrongLeader, ResistancePlotCardLocation.Hand, holder);

        var leaderCard = AddCard(ResistancePlotCardType.StrongLeader, ResistancePlotCardLocation.Hand, Leader);
        Assert.Equal(ResistanceFailure.InvalidTarget, _game.TakeLead(Leader.PlayerId, leaderCard.CardId, Now).Failure);

        var outcome = _game.TakeLead(holder.PlayerId, card.CardId, Now);

        Assert.Null(outcome.Failure);
        Assert.Equal(holder.PlayerId, _game.LeaderPlayerId);
        Assert.Equal(Leader.PlayerId, card.TargetPlayerId);
        Assert.Equal(ResistancePhase.SelectingTeam, _game.Phase);
        Assert.Equal(ResistanceFailure.CardState, _game.TakeLead(holder.PlayerId, card.CardId, Now).Failure);
    }

    [Fact]
    public void PlayNoConfidence_InTheConfidenceWindow_RejectsTheTeamAndReturnsASpotlight()
    {
        _game.Phase = ResistancePhase.ConfidenceWindow;
        var holder = _game.Players[3];
        var card = AddCard(ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.Hand, holder);
        var spotlight = AddCard(ResistancePlotCardType.InTheSpotlight, ResistancePlotCardLocation.Played, _game.Players[4]);
        spotlight.TargetPlayerId = _game.Players[1].PlayerId;
        spotlight.PlayedInMission = _game.MissionNumber;
        spotlight.IsConfirmed = true;
        _game.Players[2].PassedLeaderWindow = true;

        var outcome = _game.PlayNoConfidence(holder.PlayerId, card.CardId, Now);

        Assert.Null(outcome.Failure);
        Assert.Equal(1, _game.RejectedTeams);
        Assert.Equal(_game.Players[1].PlayerId, _game.LeaderPlayerId);
        Assert.Equal(ResistancePlotCardLocation.Hand, spotlight.Location);
        Assert.Null(spotlight.TargetPlayerId);
        Assert.False(spotlight.IsConfirmed);
        Assert.All(_game.Players, x => Assert.False(x.PassedLeaderWindow));
        Assert.Equal(Leader.PlayerId, card.TargetPlayerId);
        Assert.Equal(ResistanceFailure.CardState, _game.PlayNoConfidence(holder.PlayerId, card.CardId, Now).Failure);
    }

    [Fact]
    public void PlayNoConfidence_OutsideTheConfidenceWindow_IsRefused()
    {
        var holder = _game.Players[3];
        var card = AddCard(ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.Hand, holder);

        var outcome = _game.PlayNoConfidence(holder.PlayerId, card.CardId, Now);

        Assert.Equal(ResistanceFailure.CardNotPlayableNow, outcome.Failure);
        Assert.False(outcome.Changed);
        Assert.Equal(ResistancePlotCardLocation.Hand, card.Location);
    }

    [Fact]
    public void PeekMissionCard_AfterTheMission_IsPublicButOncePerCardAndNeverYourOwn()
    {
        _game.Phase = ResistancePhase.PeekWindow;
        var holder = _game.Players[3];
        var member = _game.Players[1];
        member.MissionCard = true;
        holder.MissionCard = false;
        var card = AddCard(ResistancePlotCardType.KeepingCloseEye, ResistancePlotCardLocation.Hand, holder);

        Assert.Equal(ResistanceFailure.InvalidTarget, _game.PeekMissionCard(holder.PlayerId, card.CardId, holder.PlayerId, Now).Failure);
        Assert.Equal(ResistanceFailure.InvalidTarget, _game.PeekMissionCard(holder.PlayerId, card.CardId, _game.Players[4].PlayerId, Now).Failure);

        Assert.Null(_game.PeekMissionCard(holder.PlayerId, card.CardId, member.PlayerId, Now).Failure);
        Assert.Equal(member.PlayerId, card.TargetPlayerId);
        Assert.True(card.IsConfirmed);
        Assert.Equal(ResistanceFailure.CardState, _game.PeekMissionCard(holder.PlayerId, card.CardId, member.PlayerId, Now).Failure);

        _game.Phase = ResistancePhase.MissionResult;
        var late = AddCard(ResistancePlotCardType.KeepingCloseEye, ResistancePlotCardLocation.Hand, holder);
        Assert.Equal(ResistanceFailure.CardNotPlayableNow, _game.PeekMissionCard(holder.PlayerId, late.CardId, member.PlayerId, Now).Failure);
    }

    [Fact]
    public void PassLeaderWindow_TheLastConfirmationEndsTheWindow()
    {
        _game.Phase = ResistancePhase.LeaderWindow;

        Assert.Equal(ResistanceFailure.NotPlayerToConfirm, _game.PassLeaderWindow(Guid.NewGuid()).Failure);
        Assert.Equal(ResistanceFailure.NotPlayerToConfirm, _game.PassLeaderWindow(null).Failure);

        foreach (var player in _game.Players.Take(4))
        {
            Assert.Null(_game.PassLeaderWindow(player.PlayerId).Failure);
            Assert.Null(_game.PassLeaderWindow(player.PlayerId).Failure);
            Assert.Equal(ResistancePhase.LeaderWindow, _game.Phase);
        }

        Assert.Null(_game.PassLeaderWindow(_game.Players[4].PlayerId).Failure);
        Assert.Equal(ResistancePhase.SelectingTeam, _game.Phase);
        Assert.All(_game.Players, x => Assert.False(x.PassedLeaderWindow));
        Assert.Equal(ResistanceFailure.CardNotPlayableNow, _game.PassLeaderWindow(Leader.PlayerId).Failure);
    }
}
