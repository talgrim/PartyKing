namespace PartyKing.Contract.V1.Player;

/// <param name="SongId">The song the host's player reported; null when idle.</param>
/// <param name="Paused">Whether that song is paused.</param>
/// <param name="PlayerConnected">Whether the host's player reported in recently.</param>
public record PlayerStateChangedDto(int? SongId, bool Paused, bool PlayerConnected);
