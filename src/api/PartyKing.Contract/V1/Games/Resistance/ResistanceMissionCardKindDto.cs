using System.Text.Json.Serialization;

namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>Serialized by name ("Success", "Fail", "Reverse"), which the UI compares against.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResistanceMissionCardKindDto
{
    Fail,
    Success,
    Reverse,
}
