using PartyKing.Domain.Entities.Games.Resistance;
using PartyKing.Domain.Entities.Games.Resistance.PlotCards;

namespace PartyKing.Domain.Tests;

/// <summary>The behavior registered for each plot card type: none may be missing, and the facts stay as the rules have them.</summary>
public class ResistancePlotCardBehaviorsTests
{
    public static TheoryData<ResistancePlotCardType> AllTypes()
    {
        return new TheoryData<ResistancePlotCardType>(Enum.GetValues<ResistancePlotCardType>());
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void EveryCardType_HasABehaviorOfItsOwn(ResistancePlotCardType type)
    {
        Assert.Equal(type, ResistancePlotCardBehaviors.For(type).Type);
    }

    [Fact]
    public void NoTwoBehaviors_ShareACardType()
    {
        var types = ResistancePlotCardBehaviors.All.Select(x => x.Type).ToList();

        Assert.Equal(Enum.GetValues<ResistancePlotCardType>().Length, types.Count);
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
        Assert.Equal(immediate, ResistancePlotCardBehaviors.For(type).IsImmediate);
        Assert.Equal(immediate, ResistancePlotCardQueries.IsImmediate(type));
    }

    [Fact]
    public void OnlyTheOpinionMaker_StartsInPlayAndOpensNoWindow()
    {
        foreach (var type in Enum.GetValues<ResistancePlotCardType>())
        {
            var isOpinionMaker = type == ResistancePlotCardType.OpinionMaker;
            var behavior = ResistancePlotCardBehaviors.For(type);

            Assert.Equal(isOpinionMaker, behavior.StartsInPlay);
            Assert.Equal(!isOpinionMaker, behavior.OpensWindows);
        }
    }

    [Fact]
    public void OnlyEstablishConfidence_IsKeptByTheLeader()
    {
        foreach (var type in Enum.GetValues<ResistancePlotCardType>())
        {
            Assert.Equal(
                type != ResistancePlotCardType.EstablishConfidence, ResistancePlotCardBehaviors.For(type).IsDealtByLeader);
        }
    }

    [Fact]
    public void OnlyInTheSpotlight_IsPlayedInTheConfidenceWindow_AndOpenAtOnce()
    {
        foreach (var type in Enum.GetValues<ResistancePlotCardType>())
        {
            var spotlight = type == ResistancePlotCardType.InTheSpotlight;
            var behavior = ResistancePlotCardBehaviors.For(type);

            Assert.Equal(spotlight, behavior.IsConfirmedOnPlay);
            Assert.Equal(
                spotlight ? ResistancePhase.ConfidenceWindow : ResistancePhase.ResolvingCards, behavior.PlayPhase);
            Assert.Equal(
                spotlight ? ResistanceFailure.NotPlayingMission : ResistanceFailure.NotResolving,
                behavior.WrongPlayPhase);
            Assert.Equal(spotlight || behavior.IsImmediate, behavior.IsPlayedOnATarget);
        }
    }

    [Fact]
    public void TheViewerOfAnIdentityCard_IsTheHolderOfOverheardConversation_AndTheTargetOfTheRest()
    {
        var holder = Guid.NewGuid();
        var target = Guid.NewGuid();

        foreach (var type in Enum.GetValues<ResistancePlotCardType>())
        {
            var card = new ResistancePlotCard { Type = type, HolderPlayerId = holder, TargetPlayerId = target };
            var overheard = type == ResistancePlotCardType.OverheardConversation;

            Assert.Equal(overheard ? holder : target, ResistancePlotCardQueries.Viewer(card));
            Assert.Equal(overheard ? target : holder, ResistancePlotCardQueries.Revealed(card));
        }
    }
}
