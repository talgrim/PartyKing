namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>
/// Which phases belong together: the questions about a group of phases that more than one place asks, answered in one
/// place. What a single phase allows stays with the action that checks it.
/// </summary>
public static class ResistancePhases
{
    /// <summary>
    /// A window in which every player confirms (or the holder of a card plays it) before the game goes on: the leader
    /// window, the confidence window and the peek window.
    /// </summary>
    public static bool IsConfirmationWindow(ResistancePhase phase)
    {
        return phase is ResistancePhase.LeaderWindow or ResistancePhase.ConfidenceWindow or ResistancePhase.PeekWindow;
    }

    /// <summary>
    /// Whether a started game in this phase can be sent back to the lobby: when it is over, and also while it waits for
    /// the Assassin, so that an absent Assassin never strands a game.
    /// </summary>
    public static bool CanRestart(ResistancePhase phase)
    {
        return phase is ResistancePhase.GameOver or ResistancePhase.Assassination;
    }

    /// <summary>
    /// The phases in which Take Responsibility may be played: between the votes (not while a team is voted on, which
    /// would change who votes first), and not in the windows that belong to other cards or in the Inquisition. The UI
    /// repeats this list (<c>PLAYABLE_PHASES</c>).
    /// </summary>
    public static IReadOnlyList<ResistancePhase> TakeResponsibility { get; } =
    [
        ResistancePhase.SelectingTeam,
        ResistancePhase.PlayingMission,
        ResistancePhase.PeekWindow,
        ResistancePhase.MissionResult,
    ];
}
