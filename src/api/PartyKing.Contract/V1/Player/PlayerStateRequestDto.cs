namespace PartyKing.Contract.V1.Player;

/// <param name="SongId">The song the host's player is playing; null when it is idle.</param>
/// <param name="Paused">Whether that song is paused.</param>
/// <param name="PositionMs">Where in the song the player is. Sent with every report so lyrics can follow along.</param>
/// <param name="Seeked">The player jumped to another position since its previous report.</param>
public record PlayerStateRequestDto(int? SongId, bool Paused, int PositionMs = 0, bool Seeked = false);
