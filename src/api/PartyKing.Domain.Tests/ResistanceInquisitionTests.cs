using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Domain.Tests;

/// <summary>The state of the Inquisitor module (<see cref="ResistanceInquisition"/>) on its own.</summary>
public class ResistanceInquisitionTests
{
    private static readonly Guid Holder = Guid.NewGuid();
    private static readonly Guid Examined = Guid.NewGuid();

    [Fact]
    public void ANewInquisition_HasNoTokenAndNobodyToAct()
    {
        var inquisition = new ResistanceInquisition();

        Assert.Null(inquisition.HolderPlayerId);
        Assert.False(inquisition.HasPicked);
        Assert.Null(inquisition.ExaminedPlayerId);
        Assert.Null(inquisition.ActingPlayerId);
    }

    [Fact]
    public void TheHolder_IsTheOneToAct_UntilTheyPick()
    {
        var inquisition = new ResistanceInquisition();

        inquisition.Hold(Holder);

        Assert.Equal(Holder, inquisition.ActingPlayerId);
        Assert.False(inquisition.HasPicked);
        Assert.Null(inquisition.ExaminedPlayerId);
    }

    [Fact]
    public void PickingAPlayer_PassesTheTokenAtOnce_AndTheExaminerIsTheOneToAct()
    {
        var inquisition = new ResistanceInquisition();
        inquisition.Hold(Holder);

        inquisition.Pick(Holder, Examined);

        Assert.Equal(Examined, inquisition.HolderPlayerId);
        Assert.Equal(Holder, inquisition.ExaminerPlayerId);
        Assert.Equal(Examined, inquisition.ExaminedPlayerId);
        Assert.Equal(Holder, inquisition.ActingPlayerId);
        Assert.True(inquisition.HasPicked);
    }

    [Fact]
    public void Finishing_KeepsTheTokenWithTheExamined_AndPassesTheTurnBackToThem()
    {
        var inquisition = new ResistanceInquisition();
        inquisition.Pick(Holder, Examined);

        inquisition.Finish();

        Assert.False(inquisition.HasPicked);
        Assert.Null(inquisition.ExaminedPlayerId);
        Assert.Equal(Examined, inquisition.HolderPlayerId);
        Assert.Equal(Examined, inquisition.ActingPlayerId);
    }

    [Fact]
    public void Clearing_TakesTheTokenAndTheExaminerAway()
    {
        var inquisition = new ResistanceInquisition();
        inquisition.Pick(Holder, Examined);

        inquisition.Clear();

        Assert.Null(inquisition.HolderPlayerId);
        Assert.Null(inquisition.ExaminerPlayerId);
        Assert.Null(inquisition.ActingPlayerId);
    }
}
