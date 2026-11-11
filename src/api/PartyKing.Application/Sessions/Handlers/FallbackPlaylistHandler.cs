using PartyKing.Application.Events;
using PartyKing.Application.Queue.Services;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Events.Sessions;

namespace PartyKing.Application.Sessions.Handlers;

/// <summary>
/// Keeps what the server remembers about a session's fallback playlist in step with it: the cached tracks and the
/// error shown to the host, and the first song of a new playlist.
/// </summary>
internal sealed class FallbackPlaylistHandler :
    IDomainEventHandler<FallbackPlaylistChanged>,
    IDomainEventHandler<FallbackPlaylistCleared>,
    IDomainEventHandler<SessionDeleted>
{
    private readonly IPlaylistTrackCache _trackCache;
    private readonly IFallbackStatusStore _statusStore;
    private readonly IFallbackQueueFiller _filler;

    public FallbackPlaylistHandler(
        IPlaylistTrackCache trackCache,
        IFallbackStatusStore statusStore,
        IFallbackQueueFiller filler)
    {
        _trackCache = trackCache;
        _statusStore = statusStore;
        _filler = filler;
    }

    public async Task HandleAsync(FallbackPlaylistChanged domainEvent, CancellationToken cancellationToken)
    {
        // Saving the same playlist again (e.g. to change the shuffle flag) must read it fresh too. A saved playlist
        // is read from the database and has nothing cached.
        if (domainEvent.Kind == FallbackPlaylistKind.Spotify)
        {
            _trackCache.Remove(domainEvent.PlaylistId);
        }

        if (domainEvent.PreviousSpotifyPlaylistId is not null)
        {
            _trackCache.Remove(domainEvent.PreviousSpotifyPlaylistId);
        }

        _statusStore.Clear(domainEvent.SessionId);

        await _filler.EnsureQueuedAsync(domainEvent.SessionId, cancellationToken);
    }

    public Task HandleAsync(FallbackPlaylistCleared domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.PreviousPlaylistId is not null)
        {
            _trackCache.Remove(domainEvent.PreviousPlaylistId);
        }

        _statusStore.Clear(domainEvent.SessionId);

        return Task.CompletedTask;
    }

    public Task HandleAsync(SessionDeleted domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.FallbackPlaylistId is not null)
        {
            _trackCache.Remove(domainEvent.FallbackPlaylistId);
        }

        _statusStore.Clear(domainEvent.SessionId);

        return Task.CompletedTask;
    }
}
