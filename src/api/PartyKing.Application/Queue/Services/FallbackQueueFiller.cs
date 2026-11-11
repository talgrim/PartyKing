using PartyKing.Application.Ports;
using PartyKing.Domain.Repositories;
using PartyKing.Application.Mappings;
using PartyKing.Application.Spotify.Services;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Queue.Services;

public interface IFallbackQueueFiller
{
    /// <summary>
    /// Keeps one song of the session's fallback playlist waiting whenever the queue has none, so "Up next" is never
    /// empty and guests' songs land behind it. Does nothing without a fallback playlist or while a song waits.
    /// With a saved playlist the songs come from the database. A failure (no host, expired login, unreachable, empty
    /// or vanished playlist) is recorded for the host, never thrown.
    /// </summary>
    Task EnsureQueuedAsync(Guid sessionId, CancellationToken cancellationToken);
}

internal class FallbackQueueFiller : IFallbackQueueFiller
{
    private readonly ISessionsRepository _sessionsRepository;
    private readonly IQueueRepository _queueRepository;
    private readonly ISpotifyIntegrationClient _spotifyClient;
    private readonly IHostTokenProvider _hostTokenProvider;
    private readonly IPlaylistTrackCache _trackCache;
    private readonly IFallbackStatusStore _statusStore;
    private readonly ISavedPlaylistsRepository _savedPlaylistsRepository;

    public FallbackQueueFiller(
        ISessionsRepository sessionsRepository,
        IQueueRepository queueRepository,
        ISpotifyIntegrationClient spotifyClient,
        IHostTokenProvider hostTokenProvider,
        IPlaylistTrackCache trackCache,
        IFallbackStatusStore statusStore,
        ISavedPlaylistsRepository savedPlaylistsRepository)
    {
        _sessionsRepository = sessionsRepository;
        _queueRepository = queueRepository;
        _spotifyClient = spotifyClient;
        _hostTokenProvider = hostTokenProvider;
        _trackCache = trackCache;
        _statusStore = statusStore;
        _savedPlaylistsRepository = savedPlaylistsRepository;
    }

    public async Task EnsureQueuedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(sessionId, cancellationToken);

        if (session?.FallbackPlaylistId is not { } playlistId || session.FallbackPlaylistKind is not { } kind)
        {
            _statusStore.Clear(sessionId);
            return;
        }

        if ((await _queueRepository.GetPendingAsync(sessionId, cancellationToken)).Count > 0)
        {
            return;
        }

        // The cursor the pick is based on: the session is a snapshot, and the playlist can be changed while it is read
        var cursorAtPick = session.FallbackCursor;

        var queued = kind == FallbackPlaylistKind.Saved
            ? await QueueSavedSongAsync(session, cursorAtPick, cancellationToken)
            : await QueueSpotifyTrackAsync(session, playlistId, cursorAtPick, cancellationToken);

        if (queued)
        {
            // The repository raised SongQueued for it, which the push handler sends
            _statusStore.Clear(sessionId);
        }
    }

    /// <summary>Queues the next track of the Spotify playlist; false when nothing was added.</summary>
    private async Task<bool> QueueSpotifyTrackAsync(
        Session session,
        string playlistId,
        int cursorAtPick,
        CancellationToken cancellationToken)
    {
        var tracks = await LoadTracksAsync(session.Id, playlistId, cancellationToken);
        if (tracks.Count == 0)
        {
            return false;
        }

        var playingUrl = await GetPlayingUrlToAvoidAsync(session, cancellationToken);

        var pick = session.NextFallbackTrack(tracks, playingUrl, Random.Shared);
        if (pick is null)
        {
            return false;
        }

        // Null when a guest queued a song while the playlist was being read: theirs goes first, and no refill is needed
        var added = await _queueRepository.AddIfNoneQueuedAsync(
            pick.Track.ToQueueItem(session.Id, isFallback: true),
            cancellationToken);

        if (added is null)
        {
            return false;
        }

        if (pick.NextCursor is { } nextCursor)
        {
            // Refused when the playlist was changed or rewound since the snapshot, which keeps the newer cursor
            await _sessionsRepository.SetFallbackCursorAsync(
                session.Id,
                playlistId,
                cursorAtPick,
                nextCursor,
                cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Queues the next song of the saved playlist, whichever source it has; false when nothing was added. The songs
    /// are in the database, so neither the host's Spotify login nor the track cache is involved.
    /// </summary>
    private async Task<bool> QueueSavedSongAsync(
        Session session,
        int cursorAtPick,
        CancellationToken cancellationToken)
    {
        if (session.FallbackSavedPlaylistId is not { } savedPlaylistId
            || await _savedPlaylistsRepository.GetAsync(savedPlaylistId, cancellationToken) is not { } playlist)
        {
            // Its account was deleted. The host sees why nothing plays, and can choose another playlist
            _statusStore.SetError(session.Id, "Fallback playlist: the saved playlist no longer exists");
            return false;
        }

        var playingUrl = await GetPlayingUrlToAvoidAsync(session, cancellationToken);

        var pick = session.NextFallbackSavedSong(playlist.Items, playingUrl, Random.Shared);
        if (pick is null)
        {
            _statusStore.SetError(session.Id, "Fallback playlist: the saved playlist has no songs");
            return false;
        }

        var added = await _queueRepository.AddIfNoneQueuedAsync(
            pick.Song.ToQueueItem(session.Id, isFallback: true),
            cancellationToken);

        if (added is null)
        {
            return false;
        }

        if (pick.NextCursor is { } nextCursor)
        {
            await _sessionsRepository.SetFallbackSavedCursorAsync(
                session.Id,
                savedPlaylistId,
                cursorAtPick,
                nextCursor,
                cancellationToken);
        }

        return true;
    }

    /// <summary>Only a shuffled playlist needs the song that plays, to avoid repeating it.</summary>
    private async Task<string?> GetPlayingUrlToAvoidAsync(Session session, CancellationToken cancellationToken)
    {
        return session.FallbackShuffle
            ? (await _queueRepository.GetPlayingAsync(session.Id, cancellationToken))?.Url
            : null;
    }

    /// <summary>The playlist's tracks, or an empty list after recording why they could not be read.</summary>
    private async Task<IReadOnlyList<Song>> LoadTracksAsync(
        Guid sessionId,
        string playlistId,
        CancellationToken cancellationToken)
    {
        if (_trackCache.TryGet(playlistId, out var cached))
        {
            return cached;
        }

        var token = await _hostTokenProvider.GetSessionAccessTokenAsync(sessionId, cancellationToken);
        if (token.IsError)
        {
            _statusStore.SetError(sessionId, $"Fallback playlist: {token.FirstError.Description}");
            return [];
        }

        IReadOnlyList<Song> tracks;
        try
        {
            tracks = await _spotifyClient.GetPlaylistTracksAsync(token.Value, playlistId, cancellationToken);
        }
        catch (Exception exception) when (exception is SpotifyApiException or HttpRequestException)
        {
            _statusStore.SetError(sessionId, "Fallback playlist: Spotify could not provide the playlist's tracks");
            return [];
        }

        if (tracks.Count == 0)
        {
            _statusStore.SetError(sessionId, "Fallback playlist: the playlist has no playable tracks");
            return [];
        }

        _trackCache.Set(playlistId, tracks);

        return tracks;
    }
}
