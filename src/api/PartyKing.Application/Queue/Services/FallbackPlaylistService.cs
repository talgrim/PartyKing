using PartyKing.Application.Ports;
using PartyKing.Domain.Repositories;
using ErrorOr;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Sessions;
using PartyKing.Application.Spotify.Services;
using PartyKing.Contract.V1.Spotify;
using PartyKing.Domain.Entities.Queue;

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

    /// <summary>
    /// Makes a saved playlist of the account the playlist played whenever the queue is empty. Only the host
    /// (<paramref name="caller"/>) or an admin may, and the playlist must be one of the account's own: another
    /// account's playlist is reported as not found.
    /// </summary>
    Task<ErrorOr<Success>> SetSavedAsync(
        Guid sessionId,
        Guid savedPlaylistId,
        bool shuffle,
        Guid accountId,
        SessionCaller caller,
        CancellationToken cancellationToken);

    /// <summary>The playlists the host owns, to choose from. Forbidden unless <paramref name="profileId"/> hosts the session.</summary>
    Task<ErrorOr<IReadOnlyList<PlaylistDto>>> GetHostPlaylistsAsync(
        Guid sessionId,
        string profileId,
        CancellationToken cancellationToken);
}

internal class FallbackPlaylistService : ManagerGuardedService, IFallbackPlaylistService
{
    private readonly ISessionsRepository _sessionsRepository;
    private readonly ISpotifyIntegrationClient _spotifyClient;
    private readonly IHostTokenProvider _hostTokenProvider;
    private readonly ISavedPlaylistsRepository _savedPlaylistsRepository;

    public FallbackPlaylistService(
        ISessionAuthorizer sessionAuthorizer,
        ISessionsRepository sessionsRepository,
        ISpotifyIntegrationClient spotifyClient,
        IHostTokenProvider hostTokenProvider,
        ISavedPlaylistsRepository savedPlaylistsRepository)
        : base(sessionAuthorizer)
    {
        _sessionsRepository = sessionsRepository;
        _spotifyClient = spotifyClient;
        _hostTokenProvider = hostTokenProvider;
        _savedPlaylistsRepository = savedPlaylistsRepository;
    }

    public Task<ErrorOr<Success>> SetAsync(
        Guid sessionId,
        string? playlist,
        bool shuffle,
        SessionCaller caller,
        CancellationToken cancellationToken)
    {
        return RunAsManagerAsync(
            sessionId,
            caller,
            SetAsyncCore,
            cancellationToken);

        async Task<ErrorOr<Success>> SetAsyncCore()
        {
            var session = await _sessionsRepository.GetAsync(sessionId, cancellationToken);
            if (session is null)
            {
                return SessionErrors.NotFound();
            }

            if (string.IsNullOrWhiteSpace(playlist))
            {
                return await ClearAsync(session.Id, cancellationToken);
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

            // The cached tracks, the old error and the first song are the FallbackPlaylistChanged handler's job
            await _sessionsRepository.SetFallbackPlaylistAsync(
                sessionId,
                found.Id,
                found.Name,
                shuffle,
                cancellationToken);

            return Result.Success;
        }
    }

    public Task<ErrorOr<Success>> SetSavedAsync(
        Guid sessionId,
        Guid savedPlaylistId,
        bool shuffle,
        Guid accountId,
        SessionCaller caller,
        CancellationToken cancellationToken)
    {
        return RunAsManagerAsync(
            sessionId,
            caller,
            SetSavedAsyncCore,
            cancellationToken);

        async Task<ErrorOr<Success>> SetSavedAsyncCore()
        {
            // Looking among the account's own playlists is the ownership check: someone else's is just not found
            var playlists = await _savedPlaylistsRepository.ListByOwnerAsync(accountId, cancellationToken);
            var chosen = playlists.FirstOrDefault(x => x.Id == savedPlaylistId);
            if (chosen is null)
            {
                return Error.NotFound(description: "The playlist was not found");
            }

            // The cached tracks of a Spotify playlist it replaces, the old error and the first song are the
            // FallbackPlaylistChanged handler's job
            var stored = await _sessionsRepository.SetFallbackSavedPlaylistAsync(
                sessionId,
                chosen.Id,
                chosen.Name,
                shuffle,
                cancellationToken);

            return stored ? Result.Success : SessionErrors.NotFound();
        }
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

        if (!session.IsHostedBy(profileId))
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

    private async Task<ErrorOr<Success>> ClearAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        // The FallbackPlaylistCleared handler drops the cached tracks and the error
        await _sessionsRepository.SetFallbackPlaylistAsync(sessionId, null, null, false, cancellationToken);

        return Result.Success;
    }
}
