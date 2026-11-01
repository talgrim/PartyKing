using PartyKing.Contract.V1.Queue;

namespace PartyKing.Application.Realtime;

/// <summary>Pushes queue deltas to the browsers that subscribed to a session.</summary>
public interface IQueueNotifier
{
    Task SongAddedAsync(Guid sessionId, QueueItemDto song, CancellationToken cancellationToken);

    Task SongRemovedAsync(Guid sessionId, int songId, CancellationToken cancellationToken);

    Task QueueAdvancedAsync(
        Guid sessionId,
        int? previousSongId,
        QueueItemDto? nowPlaying,
        CancellationToken cancellationToken);
}
