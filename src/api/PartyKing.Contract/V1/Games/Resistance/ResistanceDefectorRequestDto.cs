namespace PartyKing.Contract.V1.Games.Resistance;

/// <param name="Enabled">Whether the Defector module is played.</param>
/// <param name="KnowEachOther">
/// Whether the two Defectors know each other; omitted keeps the current choice (false for a new game).
/// </param>
public record ResistanceDefectorRequestDto(bool Enabled, bool? KnowEachOther = null);
