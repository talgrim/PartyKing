namespace PartyKing.Infrastructure.Results;

public enum ResistanceVoteStatus
{
    NoGame,
    NotStarted,
    GameOver,

    /// <summary>The caller did not join the game (a host or admin who only watches).</summary>
    NotPlayer,

    /// <summary>No team is up for a vote: it is not picked yet, or the votes are already in.</summary>
    NotVoting,

    /// <summary>The caller is an Opinion Maker and already revealed their vote, which can't be changed.</summary>
    VoteLocked,

    /// <summary>An Opinion Maker in play has not revealed their vote yet, so the others wait.</summary>
    OpinionMakersFirst,
    Voted,
}
