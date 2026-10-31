namespace PartyKing.Infrastructure.Results;

public enum ResistanceRestartStatus
{
    NoGame,
    NotStarted,

    /// <summary>The game is still being played; only a finished one restarts.</summary>
    NotOver,
    Restarted,
}
