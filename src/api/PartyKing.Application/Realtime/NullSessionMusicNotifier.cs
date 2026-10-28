using PartyKing.Contract.V1.Player;
using PartyKing.Contract.V1.Queue;

namespace PartyKing.Application.Realtime;

/// <summary>Used when no realtime transport is registered (e.g. unit tests that do not care about pushes).</summary>
internal sealed class NullSessionMusicNotifier : ISessionMusicNotifier
{
    public Task SongAddedAsync(Guid sessionId, QueueItemDto song, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task SongRemovedAsync(Guid sessionId, int songId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task QueueAdvancedAsync(
        Guid sessionId,
        int? previousSongId,
        QueueItemDto? nowPlaying,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task PlayerStateChangedAsync(
        Guid sessionId,
        PlayerStateChangedDto state,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task PlayerCommandAsync(Guid sessionId, PlayerCommandDto command, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task FallbackErrorChangedAsync(
        Guid sessionId,
        FallbackErrorDto error,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
