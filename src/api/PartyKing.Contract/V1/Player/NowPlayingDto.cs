using PartyKing.Contract.V1.Queue;

namespace PartyKing.Contract.V1.Player;

/// <param name="Song">The song being played; null when nothing is.</param>
/// <param name="Paused">Whether the host's player reported that it is paused.</param>
/// <param name="PlayerConnected">Whether the host's player reported in recently, i.e. someone is listening.</param>
public record NowPlayingDto(QueueItemDto? Song, bool Paused, bool PlayerConnected);
