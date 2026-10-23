namespace PartyKing.Contract.V1.Player;

/// <param name="SongId">The song the host's player is playing; null when it is idle.</param>
public record PlayerStateRequest(int? SongId, bool Paused);
