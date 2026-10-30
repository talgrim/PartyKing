using Microsoft.AspNetCore.SignalR;
using PartyKing.Application.Realtime;
using PartyKing.Contract.V1.Lyrics;
using PartyKing.Contract.V1.Player;
using PartyKing.Contract.V1.Queue;

namespace PartyKing.API.Hubs;

internal sealed class SignalRSessionMusicNotifier : ISessionMusicNotifier
{
    private readonly IHubContext<SessionHub> _hub;

    public SignalRSessionMusicNotifier(IHubContext<SessionHub> hub)
    {
        _hub = hub;
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

    public Task PlayerStateChangedAsync(
        Guid sessionId,
        PlayerStateChangedDto state,
        CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.PlayerStateChanged, state, cancellationToken);
    }

    public Task PlayerCommandAsync(Guid sessionId, PlayerCommandDto command, CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.PlayerCommand, command, cancellationToken);
    }

    public Task FallbackErrorChangedAsync(
        Guid sessionId,
        FallbackErrorDto error,
        CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.FallbackErrorChanged, error, cancellationToken);
    }

    public Task LyricsOffsetChangedAsync(
        Guid sessionId,
        LyricsOffsetChangedDto offset,
        CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.LyricsOffsetChanged, offset, cancellationToken);
    }

    public Task SlideshowChangedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.SlideChanged, cancellationToken);
    }

    public Task RingoChangedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.RingoChanged, cancellationToken);
    }

    public Task RingoOpenedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.RingoOpened, cancellationToken);
    }

    public Task ResistanceChangedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.ResistanceChanged, cancellationToken);
    }

    public Task ResistanceOpenedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Clients(sessionId).SendAsync(SessionMusicHubEvents.ResistanceOpened, cancellationToken);
    }

    private IClientProxy Clients(Guid sessionId)
    {
        return _hub.Clients.Group(SessionHub.GroupName(sessionId));
    }
}
