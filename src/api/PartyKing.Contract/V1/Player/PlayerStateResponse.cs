using PartyKing.Contract.V1.Enums;

namespace PartyKing.Contract.V1.Player;

/// <param name="Command">A command an admin sent since the last report; null when there is none.</param>
public record PlayerStateResponse(PlayerCommand? Command);
