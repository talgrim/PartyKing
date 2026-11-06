using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Domain.Tests;

/// <summary>
/// The order in which the plot card actions refuse a call. Ownership comes first so that nobody learns the type or the
/// state of a card that is not theirs; after it the single-purpose cards check the type and place of the card, whether it
/// was played, the phase and the target, while Play and Confirm have orders of their own.
/// </summary>
public class ResistanceGamePlotCardGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    private readonly ResistanceGame _game = NewGame();

    private ResistancePlayer Leader => _game.Players[0];

    private Guid Other => _game.Players[1].PlayerId;

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
        ResistancePlotCardType type,
        ResistancePlotCardLocation location,
        Guid? holder,
        Guid? target = null)
    {
        var card = new ResistancePlotCard
        {
            SessionId = _game.SessionId,
            CardId = _game.PlotCards.Count,
            Type = type,
            Location = location,
            HolderPlayerId = holder,
            TargetPlayerId = target,
        };
        _game.PlotCards.Add(card);

        return card;
    }

    private ResistanceOutcome Act(ResistancePlotCardType type, Guid? caller, int cardId, Guid target)
    {
        return type switch
        {
            ResistancePlotCardType.TakeResponsibility =>
                _game.TakeResponsibility(caller, cardId, target, null, Now, new Random(1)),
            ResistancePlotCardType.StrongLeader => _game.TakeLead(caller, cardId, Now),
            ResistancePlotCardType.KeepingCloseEye => _game.PeekMissionCard(caller, cardId, target, Now),
            ResistancePlotCardType.NoConfidence => _game.PlayNoConfidence(caller, cardId, Now),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    private static ResistancePhase RightPhase(ResistancePlotCardType type)
    {
        return type switch
        {
            ResistancePlotCardType.TakeResponsibility => ResistancePhase.SelectingTeam,
            ResistancePlotCardType.StrongLeader => ResistancePhase.LeaderWindow,
            ResistancePlotCardType.KeepingCloseEye => ResistancePhase.PeekWindow,
            ResistancePlotCardType.NoConfidence => ResistancePhase.ConfidenceWindow,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    // Take Responsibility is refused mid-vote, the others in any phase that is not their window
    private static ResistancePhase WrongPhase(ResistancePlotCardType type)
    {
        return type == ResistancePlotCardType.TakeResponsibility
            ? ResistancePhase.Voting
            : ResistancePhase.SelectingTeam;
    }

    [Theory]
    [InlineData(ResistancePlotCardType.TakeResponsibility)]
    [InlineData(ResistancePlotCardType.StrongLeader)]
    [InlineData(ResistancePlotCardType.KeepingCloseEye)]
    [InlineData(ResistancePlotCardType.NoConfidence)]
    public void ASingleCardAction_ChecksOwnershipBeforeAnythingElse(ResistancePlotCardType type)
    {
        // Played and in the wrong phase: every other check would refuse too, and none of them may show
        var card = AddCard(type, ResistancePlotCardLocation.Played, Leader.PlayerId);
        _game.Phase = WrongPhase(type);

        Assert.Equal(ResistanceFailure.NotYourCard, Act(type, Other, card.CardId, Other).Failure);
        Assert.Equal(ResistanceFailure.NotYourCard, Act(type, null, card.CardId, Other).Failure);
        Assert.Equal(ResistanceFailure.NotYourCard, Act(type, Leader.PlayerId, 99, Other).Failure);
    }

    [Theory]
    [InlineData(ResistancePlotCardType.TakeResponsibility, ResistancePlotCardLocation.Drawn)]
    [InlineData(ResistancePlotCardType.TakeResponsibility, ResistancePlotCardLocation.InPlay)]
    [InlineData(ResistancePlotCardType.StrongLeader, ResistancePlotCardLocation.Drawn)]
    [InlineData(ResistancePlotCardType.StrongLeader, ResistancePlotCardLocation.InPlay)]
    [InlineData(ResistancePlotCardType.KeepingCloseEye, ResistancePlotCardLocation.Drawn)]
    [InlineData(ResistancePlotCardType.KeepingCloseEye, ResistancePlotCardLocation.InPlay)]
    [InlineData(ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.Drawn)]
    [InlineData(ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.InPlay)]
    public void ASingleCardAction_RefusesACardThatIsNotInHandOrPlayed_BeforeThePhase(
        ResistancePlotCardType type, ResistancePlotCardLocation location)
    {
        var card = AddCard(type, location, Leader.PlayerId);
        _game.Phase = WrongPhase(type);

        Assert.Equal(ResistanceFailure.UnknownCard, Act(type, Leader.PlayerId, card.CardId, Other).Failure);
    }

    [Theory]
    [InlineData(ResistancePlotCardType.TakeResponsibility)]
    [InlineData(ResistancePlotCardType.StrongLeader)]
    [InlineData(ResistancePlotCardType.KeepingCloseEye)]
    [InlineData(ResistancePlotCardType.NoConfidence)]
    public void ASingleCardAction_RefusesACardOfAnotherType_BeforeThePhase(ResistancePlotCardType type)
    {
        // Open Up is a card of none of them, whichever the phase
        var card = AddCard(ResistancePlotCardType.OpenUp, ResistancePlotCardLocation.Hand, Leader.PlayerId);
        _game.Phase = WrongPhase(type);

        Assert.Equal(ResistanceFailure.UnknownCard, Act(type, Leader.PlayerId, card.CardId, Other).Failure);
    }

    [Theory]
    [InlineData(ResistancePlotCardType.TakeResponsibility, true)]
    [InlineData(ResistancePlotCardType.TakeResponsibility, false)]
    [InlineData(ResistancePlotCardType.StrongLeader, true)]
    [InlineData(ResistancePlotCardType.StrongLeader, false)]
    [InlineData(ResistancePlotCardType.KeepingCloseEye, true)]
    [InlineData(ResistancePlotCardType.KeepingCloseEye, false)]
    [InlineData(ResistancePlotCardType.NoConfidence, true)]
    [InlineData(ResistancePlotCardType.NoConfidence, false)]
    public void ASingleCardAction_RefusesAPlayedCardAsItsState_InAnyPhase(ResistancePlotCardType type, bool inItsPhase)
    {
        var card = AddCard(type, ResistancePlotCardLocation.Played, Leader.PlayerId, Other);
        _game.Phase = inItsPhase ? RightPhase(type) : WrongPhase(type);

        Assert.Equal(ResistanceFailure.CardState, Act(type, Leader.PlayerId, card.CardId, Other).Failure);
    }

    [Theory]
    [InlineData(ResistancePlotCardType.TakeResponsibility)]
    [InlineData(ResistancePlotCardType.StrongLeader)]
    [InlineData(ResistancePlotCardType.KeepingCloseEye)]
    [InlineData(ResistancePlotCardType.NoConfidence)]
    public void ASingleCardAction_ChecksThePhaseBeforeTheTarget(ResistancePlotCardType type)
    {
        var card = AddCard(type, ResistancePlotCardLocation.Hand, Leader.PlayerId);
        _game.Phase = WrongPhase(type);

        // The leader as their own target is a bad target for every card that takes one
        var outcome = Act(type, Leader.PlayerId, card.CardId, Leader.PlayerId);

        Assert.Equal(ResistanceFailure.CardNotPlayableNow, outcome.Failure);
        Assert.Equal(ResistancePlotCardLocation.Hand, card.Location);
    }

    [Theory]
    [InlineData(ResistancePlotCardType.TakeResponsibility)]
    [InlineData(ResistancePlotCardType.StrongLeader)]
    [InlineData(ResistancePlotCardType.KeepingCloseEye)]
    public void ASingleCardAction_RefusesABadTargetInItsPhase_AndChangesNothing(ResistancePlotCardType type)
    {
        // The leader holds the card, so Strong Leader would take the lead from themselves
        var card = AddCard(type, ResistancePlotCardLocation.Hand, Leader.PlayerId);
        _game.Phase = RightPhase(type);

        var outcome = Act(type, Leader.PlayerId, card.CardId, Leader.PlayerId);

        Assert.Equal(ResistanceFailure.InvalidTarget, outcome.Failure);
        Assert.False(outcome.Changed);
        Assert.Equal(ResistancePlotCardLocation.Hand, card.Location);
        Assert.Equal(Leader.PlayerId, _game.LeaderPlayerId);
    }

    [Fact]
    public void Playing_ChecksThePhaseBeforeTheStateOfThePlayedCard()
    {
        var openUp = AddCard(ResistancePlotCardType.OpenUp, ResistancePlotCardLocation.Played, Leader.PlayerId, Other);
        var spotlight = AddCard(
            ResistancePlotCardType.InTheSpotlight, ResistancePlotCardLocation.Played, Leader.PlayerId, Other);

        _game.Phase = ResistancePhase.SelectingTeam;
        Assert.Equal(ResistanceFailure.NotResolving, _game.PlayPlotCard(Leader.PlayerId, openUp.CardId, Other, Now).Failure);
        Assert.Equal(
            ResistanceFailure.NotPlayingMission, _game.PlayPlotCard(Leader.PlayerId, spotlight.CardId, Other, Now).Failure);

        _game.Phase = ResistancePhase.ResolvingCards;
        Assert.Equal(ResistanceFailure.CardState, _game.PlayPlotCard(Leader.PlayerId, openUp.CardId, Other, Now).Failure);

        _game.Phase = ResistancePhase.ConfidenceWindow;
        Assert.Equal(
            ResistanceFailure.CardState, _game.PlayPlotCard(Leader.PlayerId, spotlight.CardId, Other, Now).Failure);
    }

    [Fact]
    public void Playing_ChecksThePhaseBeforeTheTarget()
    {
        var openUp = AddCard(ResistancePlotCardType.OpenUp, ResistancePlotCardLocation.Hand, Leader.PlayerId);
        var spotlight = AddCard(ResistancePlotCardType.InTheSpotlight, ResistancePlotCardLocation.Hand, Leader.PlayerId);

        // Themselves as the target is a bad one for both
        _game.Phase = ResistancePhase.SelectingTeam;
        Assert.Equal(
            ResistanceFailure.NotResolving, _game.PlayPlotCard(Leader.PlayerId, openUp.CardId, Leader.PlayerId, Now).Failure);
        Assert.Equal(
            ResistanceFailure.NotPlayingMission,
            _game.PlayPlotCard(Leader.PlayerId, spotlight.CardId, Leader.PlayerId, Now).Failure);

        _game.Phase = ResistancePhase.ResolvingCards;
        Assert.Equal(
            ResistanceFailure.InvalidTarget, _game.PlayPlotCard(Leader.PlayerId, openUp.CardId, Leader.PlayerId, Now).Failure);
    }

    [Fact]
    public void Playing_ChecksOwnershipBeforeThePhase_AndThePhaseBeforeTheTypeAndPlace()
    {
        var openUp = AddCard(ResistancePlotCardType.OpenUp, ResistancePlotCardLocation.Drawn, Leader.PlayerId);
        _game.Phase = ResistancePhase.SelectingTeam;

        Assert.Equal(ResistanceFailure.NotYourCard, _game.PlayPlotCard(Other, openUp.CardId, Leader.PlayerId, Now).Failure);
        Assert.Equal(
            ResistanceFailure.NotResolving, _game.PlayPlotCard(Leader.PlayerId, openUp.CardId, Other, Now).Failure);

        _game.Phase = ResistancePhase.ResolvingCards;
        Assert.Equal(ResistanceFailure.UnknownCard, _game.PlayPlotCard(Leader.PlayerId, openUp.CardId, Other, Now).Failure);
    }

    [Fact]
    public void Confirming_ChecksThePhaseBeforeOwnership_ThenTheTypeAndPlace()
    {
        var noConfidence = AddCard(ResistancePlotCardType.NoConfidence, ResistancePlotCardLocation.Hand, Leader.PlayerId);
        var drawn = AddCard(ResistancePlotCardType.OpenUp, ResistancePlotCardLocation.Drawn, Leader.PlayerId);

        // Outside the step even a stranger and an unknown id get the phase
        _game.Phase = ResistancePhase.SelectingTeam;
        Assert.Equal(ResistanceFailure.NotResolving, _game.ConfirmPlotCard(Other, noConfidence.CardId).Failure);
        Assert.Equal(ResistanceFailure.NotResolving, _game.ConfirmPlotCard(Other, 99).Failure);

        _game.Phase = ResistancePhase.ResolvingCards;
        Assert.Equal(ResistanceFailure.NotYourCard, _game.ConfirmPlotCard(Other, noConfidence.CardId).Failure);
        Assert.Equal(ResistanceFailure.NotYourCard, _game.ConfirmPlotCard(Other, 99).Failure);
        Assert.Equal(ResistanceFailure.UnknownCard, _game.ConfirmPlotCard(Leader.PlayerId, noConfidence.CardId).Failure);
        Assert.Equal(ResistanceFailure.UnknownCard, _game.ConfirmPlotCard(Leader.PlayerId, drawn.CardId).Failure);
    }

    [Fact]
    public void Confirming_AnOpenUpIsLeftToItsTarget_NotToItsHolder()
    {
        _game.Phase = ResistancePhase.ResolvingCards;
        var openUp = AddCard(ResistancePlotCardType.OpenUp, ResistancePlotCardLocation.Played, Leader.PlayerId, Other);

        // The holder passes the ownership check, but the identity card is shown to the target
        var byHolder = _game.ConfirmPlotCard(Leader.PlayerId, openUp.CardId);

        Assert.Equal(ResistanceFailure.NotYourCard, byHolder.Failure);
        Assert.False(openUp.IsConfirmed);
        Assert.Null(_game.ConfirmPlotCard(Other, openUp.CardId).Failure);
        Assert.True(openUp.IsConfirmed);
    }
}
