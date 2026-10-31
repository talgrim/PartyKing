namespace PartyKing.Infrastructure.Results;

public enum ResistanceMissionStatus
{
    NoGame,
    NotStarted,
    GameOver,

    /// <summary>The caller did not join the game (a host or admin who only watches).</summary>
    NotPlayer,

    /// <summary>No approved team is playing a mission.</summary>
    NotPlaying,

    /// <summary>The caller is not on the team.</summary>
    NotOnTeam,
    AlreadyPlayed,

    /// <summary>A member of the resistance may only play Success.</summary>
    MustSucceed,
    Played,
}
