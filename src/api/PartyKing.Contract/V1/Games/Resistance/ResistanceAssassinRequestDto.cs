namespace PartyKing.Contract.V1.Games.Resistance;

/// <param name="Enabled">Whether the Assassin module is played; off also switches the optional characters off.</param>
/// <param name="BodyGuard">Whether a Body Guard is dealt; omitted keeps the choice (false for a new game).</param>
/// <param name="DeepCover">Whether a Deep Cover spy is dealt; omitted keeps the choice.</param>
/// <param name="BlindSpy">Whether a Blind Spy is dealt; omitted keeps the choice.</param>
/// <param name="FalseCommander">Whether a False Commander is dealt; omitted keeps the choice.</param>
public record ResistanceAssassinRequestDto(
    bool Enabled,
    bool? BodyGuard = null,
    bool? DeepCover = null,
    bool? BlindSpy = null,
    bool? FalseCommander = null);
