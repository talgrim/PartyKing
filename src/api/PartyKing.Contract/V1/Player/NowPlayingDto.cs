using PartyKing.Contract.V1.Queue;

namespace PartyKing.Contract.V1.Player;

/// <param name="Song">The song being played; null when nothing is.</param>
/// <param name="Paused">Whether the host's player reported that it is paused.</param>
/// <param name="PlayerConnected">Whether the host's player reported in recently, i.e. someone is listening.</param>
/// <param name="PositionMs">Where in the song the player is as of this response; 0 when it is not known for this song.</param>
public record NowPlayingDto(QueueItemDto? Song, bool Paused, bool PlayerConnected, int PositionMs = 0);
