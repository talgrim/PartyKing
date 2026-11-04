using PartyKing.Application.Ports;
using PartyKing.Domain.Repositories;
using PartyKing.Application.Mappings;
using PartyKing.Application.Spotify.Services;
using PartyKing.Domain.Entities.Spotify;

namespace PartyKing.Application.Queue.Services;

public interface IFallbackQueueFiller
{
    /// <summary>
    /// Keeps one song of the session's fallback playlist waiting whenever the queue has none, so "Up next" is never
    /// empty and guests' songs land behind it. Does nothing without a fallback playlist or while a song waits.
    /// A failure (no host, expired login, unreachable or empty playlist) is recorded for the host, never thrown.
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

    public FallbackQueueFiller(
        ISessionsRepository sessionsRepository,
        IQueueRepository queueRepository,
        ISpotifyIntegrationClient spotifyClient,
        IHostTokenProvider hostTokenProvider,
        IPlaylistTrackCache trackCache,
        IFallbackStatusStore statusStore)
    {
        _sessionsRepository = sessionsRepository;
        _queueRepository = queueRepository;
        _spotifyClient = spotifyClient;
        _hostTokenProvider = hostTokenProvider;
        _trackCache = trackCache;
        _statusStore = statusStore;
    }

    public async Task EnsureQueuedAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(sessionId, cancellationToken);

        if (session?.FallbackPlaylistId is not { } playlistId)
        {
            _statusStore.Clear(sessionId);
            return;
        }

        if ((await _queueRepository.GetPendingAsync(sessionId, cancellationToken)).Count > 0)
        {
            return;
        }

        // The cursor the pick is based on: the session is a snapshot, and the playlist can be changed while Spotify is read
        var cursorAtPick = session.FallbackCursor;

        var tracks = await LoadTracksAsync(sessionId, playlistId, cancellationToken);
        if (tracks.Count == 0)
        {
            return;
        }

        // Only a shuffled playlist needs the song that plays, to avoid repeating it
        var playingUrl = session.FallbackShuffle
            ? (await _queueRepository.GetPlayingAsync(sessionId, cancellationToken))?.Url
            : null;

        var pick = session.NextFallbackTrack(tracks, playingUrl, Random.Shared);
        if (pick is null)
        {
            return;
        }

        // Null when a guest queued a song while the playlist was being read: theirs goes first, and no refill is needed
        var added = await _queueRepository.AddIfNoneQueuedAsync(
            pick.Track.ToQueueItem(sessionId, isFallback: true),
            cancellationToken);

        if (added is null)
        {
            return;
        }

        if (pick.NextCursor is { } nextCursor)
        {
            // Refused when the playlist was changed or rewound since the snapshot, which keeps the newer cursor
            await _sessionsRepository.SetFallbackCursorAsync(
                sessionId,
                playlistId,
                cursorAtPick,
                nextCursor,
                cancellationToken);
        }

        // The repository raised SongQueued for it, which the push handler sends
        _statusStore.Clear(sessionId);
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
