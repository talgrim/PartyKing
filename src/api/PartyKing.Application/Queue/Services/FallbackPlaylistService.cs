using PartyKing.Application.Ports;
using PartyKing.Domain.Repositories;
using ErrorOr;
using PartyKing.Application.Queue.Helpers;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Sessions;
using PartyKing.Application.Spotify.Services;
using PartyKing.Contract.V1.Spotify;

namespace PartyKing.Application.Queue.Services;

public interface IFallbackPlaylistService
{
    /// <summary>
    /// Sets the playlist played whenever the queue is empty, or clears it when <paramref name="playlist"/> is empty.
    /// Only the host (<paramref name="caller"/>) or an admin may; the playlist must belong to the session's host.
    /// </summary>
    Task<ErrorOr<Success>> SetAsync(
        Guid sessionId,
        string? playlist,
        bool shuffle,
        SessionCaller caller,
        CancellationToken cancellationToken);

    /// <summary>The playlists the host owns, to choose from. Forbidden unless <paramref name="profileId"/> hosts the session.</summary>
    Task<ErrorOr<IReadOnlyList<PlaylistDto>>> GetHostPlaylistsAsync(
        Guid sessionId,
        string profileId,
        CancellationToken cancellationToken);
}

internal class FallbackPlaylistService : IFallbackPlaylistService
{
    private readonly ISessionAuthorizer _sessionAuthorizer;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly ISpotifyIntegrationClient _spotifyClient;
    private readonly IHostTokenProvider _hostTokenProvider;
    private readonly IFallbackQueueFiller _filler;
    private readonly IPlaylistTrackCache _trackCache;
    private readonly IFallbackStatusStore _statusStore;

    public FallbackPlaylistService(
        ISessionAuthorizer sessionAuthorizer,
        ISessionsRepository sessionsRepository,
        ISpotifyIntegrationClient spotifyClient,
        IHostTokenProvider hostTokenProvider,
        IFallbackQueueFiller filler,
        IPlaylistTrackCache trackCache,
        IFallbackStatusStore statusStore)
    {
        _sessionAuthorizer = sessionAuthorizer;
        _sessionsRepository = sessionsRepository;
        _spotifyClient = spotifyClient;
        _hostTokenProvider = hostTokenProvider;
        _filler = filler;
        _trackCache = trackCache;
        _statusStore = statusStore;
    }

    public Task<ErrorOr<Success>> SetAsync(
        Guid sessionId,
        string? playlist,
        bool shuffle,
        SessionCaller caller,
        CancellationToken cancellationToken)
    {
        return _sessionAuthorizer.RunAsManagerAsync(
            sessionId,
            caller,
            () => SetCoreAsync(sessionId, playlist, shuffle, cancellationToken),
            cancellationToken);
    }

    private async Task<ErrorOr<Success>> SetCoreAsync(
        Guid sessionId,
        string? playlist,
        bool shuffle,
        CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return SessionErrors.NotFound();
        }

        if (string.IsNullOrWhiteSpace(playlist))
        {
            return await ClearAsync(session.Id, session.FallbackPlaylistId, cancellationToken);
        }

        var playlistId = PlaylistInputParser.Parse(playlist);
        if (playlistId is null)
        {
            return Error.Validation(description: "Expected a Spotify playlist link or a spotify:playlist: URI");
        }

        var token = await _hostTokenProvider.GetSessionAccessTokenAsync(sessionId, cancellationToken);
        if (token.IsError)
        {
            return token.FirstError;
        }

        var found = await _spotifyClient.GetPlaylistAsync(token.Value, playlistId, cancellationToken);
        if (found is null)
        {
            return Error.NotFound(description: "The playlist was not found");
        }

        if (found.OwnerId != session.HostProfileId)
        {
            return Error.Validation(description: "Only playlists the host owns can be used");
        }

        if (found.TrackCount == 0)
        {
            return Error.Validation(description: "The playlist is empty");
        }

        await _sessionsRepository.SetFallbackPlaylistAsync(sessionId, found.Id, found.Name, shuffle, cancellationToken);
        _trackCache.Remove(found.Id);
        _statusStore.Clear(sessionId);

        await _filler.EnsureQueuedAsync(sessionId, cancellationToken);

        return Result.Success;
    }

    public async Task<ErrorOr<IReadOnlyList<PlaylistDto>>> GetHostPlaylistsAsync(
        Guid sessionId,
        string profileId,
        CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return SessionErrors.NotFound();
        }

        if (session.HostProfileId != profileId)
        {
            return Error.Forbidden(description: "Only the host of this session can list their playlists");
        }

        var token = await _hostTokenProvider.GetSessionAccessTokenAsync(sessionId, cancellationToken);
        if (token.IsError)
        {
            return token.FirstError;
        }

        try
        {
            var playlists = await _spotifyClient.GetCurrentUserPlaylistsAsync(token.Value, cancellationToken);

            return playlists
                .Where(x => x.OwnerId == profileId)
                .Select(x => new PlaylistDto($"spotify:playlist:{x.Id}", x.Name, x.ImageUrl, x.TrackCount))
                .ToList();
        }
        catch (SpotifyApiException exception) when (exception.StatusCode is global::System.Net.HttpStatusCode.Forbidden)
        {
            // The host logged in before the playlist scope was requested
            return Error.Conflict(description: "Spotify did not allow reading your playlists. Log in as the host again");
        }
    }

    private async Task<ErrorOr<Success>> ClearAsync(Guid sessionId, string? playlistId, CancellationToken cancellationToken)
    {
        await _sessionsRepository.SetFallbackPlaylistAsync(sessionId, null, null, false, cancellationToken);
        _statusStore.Clear(sessionId);

        if (playlistId is not null)
        {
            _trackCache.Remove(playlistId);
        }

        return Result.Success;
    }
}
