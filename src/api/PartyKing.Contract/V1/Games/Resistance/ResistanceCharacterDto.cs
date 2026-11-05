using System.Text.Json.Serialization;

namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>Serialized by name ("Standard", "Reverser", "Defector", "Commander", "Assassin", "BodyGuard", "DeepCover", "BlindSpy", "FalseCommander"), which the UI compares against.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResistanceCharacterDto
{
    Standard,
    Reverser,
    Defector,
    Commander,
    Assassin,
    BodyGuard,
    DeepCover,
    BlindSpy,
    FalseCommander,
}
