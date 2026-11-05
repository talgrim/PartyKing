namespace PartyKing.Contract.V1.Games.Resistance;

/// <summary>Switches the Inquisitor module on or off (only before the game starts).</summary>
public record ResistanceInquisitorRequestDto(bool Enabled);
