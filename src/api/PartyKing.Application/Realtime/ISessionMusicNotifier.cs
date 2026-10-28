using PartyKing.Contract.V1.Player;
using PartyKing.Contract.V1.Queue;

namespace PartyKing.Application.Realtime;

/// <summary>
/// Pushes music-queue and player-state deltas to browsers that subscribed to a session. Implementations may no-op
/// (tests) or fan out through SignalR.
/// </summary>
public interface ISessionMusicNotifier
{
    Task SongAddedAsync(Guid sessionId, QueueItemDto song, CancellationToken cancellationToken);

    Task SongRemovedAsync(Guid sessionId, int songId, CancellationToken cancellationToken);

    Task QueueAdvancedAsync(
        Guid sessionId,
        int? previousSongId,
        QueueItemDto? nowPlaying,
        CancellationToken cancellationToken);

    Task PlayerStateChangedAsync(
        Guid sessionId,
        PlayerStateChangedDto state,
        CancellationToken cancellationToken);

    Task PlayerCommandAsync(Guid sessionId, PlayerCommandDto command, CancellationToken cancellationToken);

    Task FallbackErrorChangedAsync(Guid sessionId, FallbackErrorDto error, CancellationToken cancellationToken);
}
