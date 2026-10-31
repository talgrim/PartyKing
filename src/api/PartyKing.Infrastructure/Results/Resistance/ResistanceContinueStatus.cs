namespace PartyKing.Infrastructure.Results;

public enum ResistanceContinueStatus
{
    NoGame,
    NotStarted,
    GameOver,

    /// <summary>Neither the leader nor a manager.</summary>
    NotAllowed,

    /// <summary>The votes are not in yet, or the approved team's mission is still being played.</summary>
    NothingToContinue,
    Continued,
}
