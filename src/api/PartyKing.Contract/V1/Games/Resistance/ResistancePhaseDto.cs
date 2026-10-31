namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>Serialized by name, which the UI compares against.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResistancePhaseDto
{
    SelectingTeam,
    Voting,
    VoteResult,
    GameOver,
    PlayingMission,
    MissionResult,
    DealingCards,
    ResolvingCards,
    LeaderWindow,
    ConfidenceWindow,
    PeekWindow,
}
