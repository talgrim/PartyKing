using PartyKing.Contract.V1.Lyrics;
using PartyKing.Contract.V1.Player;
using PartyKing.Contract.V1.Queue;

namespace PartyKing.Application.Realtime;

// Used when no realtime transport is registered (e.g. unit tests that do not care about pushes).

internal sealed class NullQueueNotifier : IQueueNotifier
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
}

internal sealed class NullPlayerNotifier : IPlayerNotifier
{
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

internal sealed class NullLyricsNotifier : ILyricsNotifier
{
    public Task LyricsOffsetChangedAsync(
        Guid sessionId,
        LyricsOffsetChangedDto offset,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

internal sealed class NullSlideshowNotifier : ISlideshowNotifier
{
    public Task SlideshowChangedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

internal sealed class NullRingoNotifier : IRingoNotifier
{
    public Task OpenedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task ChangedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

internal sealed class NullResistanceNotifier : IResistanceNotifier
{
    public Task OpenedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task ChangedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
