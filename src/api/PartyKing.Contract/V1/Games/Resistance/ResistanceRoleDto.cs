using System.Text.Json.Serialization;

namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>Serialized by name ("Resistance", "Spy"), which the UI compares against.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResistanceRoleDto
{
    Resistance,
    Spy,
}
