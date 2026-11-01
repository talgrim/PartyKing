using System.Text.Json.Serialization;

namespace PartyKing.Domain.Entities.Games.Resistance;

/// <summary>The plot cards of the optional Resistance add-on. Stored by name in the rules snapshot.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResistancePlotCardType
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
