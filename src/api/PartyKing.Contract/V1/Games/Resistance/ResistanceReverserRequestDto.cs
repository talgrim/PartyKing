namespace PartyKing.Contract.V1.Games.Resistance;

/// <param name="Enabled">Whether the Reverser module is played.</param>
/// <param name="Sides">Which sides get a Reverser; omitted keeps the current choice (Both for a new game).</param>
/// <param name="SpyKnowsSpies">
/// Whether a spy Reverser and the other spies know each other; omitted keeps the current choice (true for a new game).
/// </param>
public record ResistanceReverserRequestDto(
    bool Enabled, ResistanceReverserSidesDto? Sides = null, bool? SpyKnowsSpies = null);
