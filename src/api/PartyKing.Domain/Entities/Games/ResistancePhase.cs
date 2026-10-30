namespace PartyKing.Domain.Entities.Games;

/// <summary>Where a started Resistance game is in the round of the current mission.</summary>
public enum ResistancePhase
{
    /// <summary>The leader picks the team.</summary>
    SelectingTeam,

    /// <summary>The team is submitted and the players vote; a vote can be changed until the last player has voted.</summary>
    Voting,

    /// <summary>No longer used: a rejected team passes the lead on at once. Kept so the stored numbers stay stable.</summary>
    VoteResult,

    /// <summary>The game is decided (<see cref="ResistanceGame.Winner"/>).</summary>
    GameOver,

    /// <summary>The team was approved and its members secretly play a Success or Fail card.</summary>
    PlayingMission,

    /// <summary>All cards are in and the outcome of the mission is public. It moves on when someone continues.</summary>
    MissionResult,

    /// <summary>
    /// Plot cards add-on: the mission's leader drew cards and hands them to the other players before picking the
    /// team. Only entered at the start of a mission, not after a rejected team.
    /// </summary>
    DealingCards,
}
