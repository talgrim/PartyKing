namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>Serialized by name, which the UI compares against.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResistancePlotCardTypeDto
{
    OverheardConversation,
    OpinionMaker,
    EstablishConfidence,
    OpenUp,
    NoConfidence,
    KeepingCloseEye,
    StrongLeader,
    InTheSpotlight,
    TakeResponsibility,
}
