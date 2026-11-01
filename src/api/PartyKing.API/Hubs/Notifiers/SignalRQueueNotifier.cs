using Microsoft.AspNetCore.SignalR;
using PartyKing.Application.Realtime;
using PartyKing.Contract.V1.Queue;

namespace PartyKing.API.Hubs.Notifiers;

internal sealed class SignalRQueueNotifier : SessionGroupNotifier, IQueueNotifier
{
    public SignalRQueueNotifier(IHubContext<SessionHub> hub) : base(hub)
    {
    }

    public Task SongAddedAsync(Guid sessionId, QueueItemDto song, CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.SongAdded, song, cancellationToken);
    }

    public Task SongRemovedAsync(Guid sessionId, int songId, CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(
            SessionMusicHubEvents.SongRemoved,
            new QueueItemRemovedDto(songId),
            cancellationToken);
    }

    public Task QueueAdvancedAsync(
        Guid sessionId,
        int? previousSongId,
        QueueItemDto? nowPlaying,
        CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(
            SessionMusicHubEvents.QueueAdvanced,
            new QueueAdvancedDto(previousSongId, nowPlaying),
            cancellationToken);
    }
}
