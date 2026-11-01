namespace PartyKing.Domain.Entities.Games.Resistance;

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

    /// <summary>
    /// Plot cards add-on: the immediate cards (Overheard Conversation, Establish Confidence, Open Up) are played, in
    /// any order, and their receivers confirm what they saw. Moves on to picking the team when all are confirmed.
    /// </summary>
    ResolvingCards,

    /// <summary>
    /// Plot cards add-on: some playable card is in a hand, so before the leader does anything (drawing cards,
    /// picking the team) every player confirms they play no card, or a Strong Leader's holder takes the lead. Opened
    /// for any playable hand card (not Opinion Maker), so it never tells which card is out there. Moves on when all
    /// confirmed or one was played.
    /// </summary>
    LeaderWindow,

    /// <summary>
    /// Plot cards add-on: the team was approved while a playable card sits in a hand, so everyone confirms they play
    /// none, or a No Confidence's holder cancels the approval, or an In the Spotlight's holder names who plays openly.
    /// Like <see cref="LeaderWindow"/> it opens for any such card.
    /// </summary>
    ConfidenceWindow,

    /// <summary>
    /// Plot cards add-on: the mission is played and its outcome is public, but some playable card sits in a hand, so
    /// everyone confirms they play none (Keeping a Close Eye is played here) before "Dalej" appears. Opens for any
    /// playable card so it never tells who holds one.
    /// </summary>
    PeekWindow,
}
