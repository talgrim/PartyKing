using PartyKing.Contract.V1.Enums;

namespace PartyKing.Contract.V1.Player;

/// <param name="Command">What an admin asked the host's player to do.</param>
public record PlayerCommandDto(PlayerCommand Command);
