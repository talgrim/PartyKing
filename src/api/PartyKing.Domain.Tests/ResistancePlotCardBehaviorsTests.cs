using PartyKing.Domain.Entities.Games.Resistance;
using PartyKing.Domain.Entities.Games.Resistance.PlotCards;

namespace PartyKing.Domain.Tests;

/// <summary>The behavior of each plot card type: none may be missing or throw, and the facts stay as the rules have them.</summary>
public class ResistancePlotCardBehaviorsTests
{
    private static readonly ResistancePlotCardType[] Types = Enum.GetValues<ResistancePlotCardType>();

    public static TheoryData<ResistancePlotCardType> AllTypes()
    {
        return new TheoryData<ResistancePlotCardType>(Types);
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void EveryCardType_HasABehaviorOfItsOwn(ResistancePlotCardType type)
    {
        Assert.Equal(type, ResistancePlotCardBehaviors.For(type).Type);
    }

    [Fact]
    public void ATypeNobodyRegistered_IsAPlainCard_AndNeverThrows()
    {
        var unknown = (ResistancePlotCardType)999;

        var behavior = ResistancePlotCardBehaviors.For(unknown);

        Assert.Equal(unknown, behavior.Type);
        Assert.True(behavior.IsDealtByLeader);
        Assert.False(behavior.StartsInPlay);
        Assert.IsNotAssignableFrom<IPlayedOnATargetBehavior>(behavior);
    }

    [Fact]
    public void NoTwoRegisteredBehaviors_ShareACardType()
    {
        var types = ResistancePlotCardBehaviors.Registered.Select(x => x.Type).ToList();

        Assert.Equal(types.Count, types.Distinct().Count());
    }

    [Theory]
    [InlineData(ResistancePlotCardType.OverheardConversation, true)]
    [InlineData(ResistancePlotCardType.EstablishConfidence, true)]
    [InlineData(ResistancePlotCardType.OpenUp, true)]
    [InlineData(ResistancePlotCardType.OpinionMaker, false)]
    [InlineData(ResistancePlotCardType.NoConfidence, false)]
    [InlineData(ResistancePlotCardType.KeepingCloseEye, false)]
    [InlineData(ResistancePlotCardType.StrongLeader, false)]
    [InlineData(ResistancePlotCardType.InTheSpotlight, false)]
    [InlineData(ResistancePlotCardType.TakeResponsibility, false)]
    public void OnlyThreeCardsAreImmediate(ResistancePlotCardType type, bool immediate)
    {
        Assert.Equal(immediate, ResistancePlotCardBehaviors.For(type) is IImmediateBehavior);
        Assert.Equal(immediate, ResistancePlotCardQueries.IsImmediate(type));
    }

    [Fact]
    public void OnlyTheOpinionMaker_StartsInPlay()
    {
        foreach (var type in Types)
        {
            Assert.Equal(
                type == ResistancePlotCardType.OpinionMaker, ResistancePlotCardBehaviors.For(type).StartsInPlay);
        }
    }

    [Fact]
    public void OnlyEstablishConfidence_IsKeptByTheLeader()
    {
        foreach (var type in Types)
        {
            Assert.Equal(
                type != ResistancePlotCardType.EstablishConfidence, ResistancePlotCardBehaviors.For(type).IsDealtByLeader);
        }
    }

    [Fact]
    public void TheImmediateCardsAndInTheSpotlight_AreThePlayedOnATarget()
    {
        foreach (var type in Types)
        {
            var behavior = ResistancePlotCardBehaviors.For(type);
            var expected = type == ResistancePlotCardType.InTheSpotlight || behavior is IImmediateBehavior;

            Assert.Equal(expected, behavior is IPlayedOnATargetBehavior);
        }
    }

    [Fact]
    public void OnlyInTheSpotlight_IsPlayedInTheConfidenceWindow_AndOpenAtOnce()
    {
        foreach (var type in Types)
        {
            if (ResistancePlotCardBehaviors.For(type) is not IPlayedOnATargetBehavior play)
            {
                continue;
            }

            var spotlight = type == ResistancePlotCardType.InTheSpotlight;

            Assert.Equal(spotlight, play.IsConfirmedOnPlay);
            Assert.Equal(spotlight ? ResistancePhase.ConfidenceWindow : ResistancePhase.ResolvingCards, play.PlayPhase);
            Assert.Equal(
                spotlight ? ResistanceFailure.NotPlayingMission : ResistanceFailure.NotResolving, play.WrongPlayPhase);
        }
    }

    [Fact]
    public void TheViewerOfAnIdentityCard_IsTheHolderOfOverheardConversation_AndTheTargetOfTheRest()
    {
        var holder = Guid.NewGuid();
        var target = Guid.NewGuid();

        foreach (var type in Types)
        {
            var card = new ResistancePlotCard { Type = type, HolderPlayerId = holder, TargetPlayerId = target };
            var overheard = type == ResistancePlotCardType.OverheardConversation;

            Assert.Equal(overheard ? holder : target, ResistancePlotCardQueries.Viewer(card));
            Assert.Equal(overheard ? target : holder, ResistancePlotCardQueries.Revealed(card));
        }
    }
}
