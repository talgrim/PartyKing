using PartyKing.Contract.V1.Enums;

namespace PartyKing.Contract.V1.Player;

/// <param name="Command">A command an admin sent since the last report; null when there is none.</param>
/// <param name="FallbackError">Why the fallback playlist could not add a song; null when it works or is not set.</param>
public record PlayerStateResponse(PlayerCommand? Command, string? FallbackError = null);
