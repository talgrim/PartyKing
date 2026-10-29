namespace PartyKing.Domain.Entities.Games;

/// <summary>Where a started Resistance game is in the round of the current mission.</summary>
public enum ResistancePhase
{
    /// <summary>The leader picks the team.</summary>
    SelectingTeam,

    /// <summary>The team is submitted and the players vote; a vote can be changed until the last player has voted.</summary>
    Voting,

    /// <summary>Everybody voted and the votes are public. A rejected team moves on when someone continues.</summary>
    VoteResult,

    /// <summary>The game is decided (<see cref="ResistanceGame.Winner"/>).</summary>
    GameOver,
}
