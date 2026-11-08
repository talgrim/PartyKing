using PartyKing.Domain.Entities.Games.Resistance;

namespace PartyKing.Domain.Tests;

/// <summary>The groups of phases (<see cref="ResistancePhases"/>), over every phase there is.</summary>
public class ResistancePhasesTests
{
    private static readonly ResistancePhase[] AllPhases = Enum.GetValues<ResistancePhase>();

    [Fact]
    public void TheConfirmationWindows_AreExactlyTheLeaderTheConfidenceAndThePeekWindow()
    {
        var windows = AllPhases.Where(ResistancePhases.IsConfirmationWindow);

        Assert.Equal(
            [ResistancePhase.LeaderWindow, ResistancePhase.ConfidenceWindow, ResistancePhase.PeekWindow],
            windows.OrderBy(x => x));
    }

    [Fact]
    public void AGameCanBeRestarted_OnlyWhenItIsOverOrWaitsForTheAssassin()
    {
        var restartable = AllPhases.Where(ResistancePhases.CanRestart);

        Assert.Equal([ResistancePhase.GameOver, ResistancePhase.Assassination], restartable.OrderBy(x => x));
    }

    [Fact]
    public void TakeResponsibility_IsPlayableBetweenTheVotesOnly()
    {
        Assert.Equal(
            [
                ResistancePhase.SelectingTeam, ResistancePhase.PlayingMission, ResistancePhase.PeekWindow,
                ResistancePhase.MissionResult,
            ],
            ResistancePhases.TakeResponsibility);
    }
}
