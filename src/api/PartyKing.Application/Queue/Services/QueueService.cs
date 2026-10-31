using ErrorOr;
using Microsoft.Extensions.Logging;
using PartyKing.Application.Mappings;
using PartyKing.Application.Queue.Helpers;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Application.Spotify.Services;
using PartyKing.Contract.V1.Queue;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Results;
using PartyKing.Infrastructure.Spotify.RestApi;
using PartyKing.Infrastructure.Youtube.RestApi;

namespace PartyKing.Application.Queue.Services;

public interface IQueueService
{
    Task<ErrorOr<QueueItemDto>> EnqueueAsync(Guid sessionId, string? input, CancellationToken cancellationToken);
    Task<ErrorOr<IReadOnlyList<QueueItemDto>>> GetQueueAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Host only: forbidden unless <paramref name="hostProfileId"/> hosts the session. <paramref name="currentSongId"/>
    /// is the song the player thinks is playing (null: none); when it is outdated, nothing advances and the song
    /// that is really playing is returned.
    /// </summary>
    Task<ErrorOr<QueueItemDto>> GetNextAsync(
        Guid sessionId,
        string hostProfileId,
        int? currentSongId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes a waiting song. Forbidden unless the caller is an admin or <paramref name="caller"/> hosts the
    /// session; conflict when the song is no longer waiting (it is playing or has been played).
    /// </summary>
    Task<ErrorOr<Success>> RemoveAsync(
        Guid sessionId,
        int songId,
        SessionCaller caller,
        CancellationToken cancellationToken);
}

internal class QueueService : IQueueService
{
    private readonly IQueueRepository _queueRepository;
    private readonly ISpotifyIntegrationClient _spotifyClient;
    private readonly IYoutubeOEmbedClient _youtubeClient;
    private readonly IHostTokenProvider _hostTokenProvider;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly ISessionMusicNotifier _musicNotifier;
    private readonly IFallbackQueueFiller _fallbackFiller;
    private readonly ILogger<QueueService> _logger;

    public QueueService(
        IQueueRepository queueRepository,
        ISessionsRepository sessionsRepository,
        ISpotifyIntegrationClient spotifyClient,
        IYoutubeOEmbedClient youtubeClient,
        IHostTokenProvider hostTokenProvider,
        ISessionMusicNotifier musicNotifier,
        IFallbackQueueFiller fallbackFiller,
        ILogger<QueueService> logger)
    {
        _queueRepository = queueRepository;
        _sessionsRepository = sessionsRepository;
        _spotifyClient = spotifyClient;
        _youtubeClient = youtubeClient;
        _hostTokenProvider = hostTokenProvider;
        _musicNotifier = musicNotifier;
        _fallbackFiller = fallbackFiller;
        _logger = logger;
    }

    public async Task<ErrorOr<QueueItemDto>> EnqueueAsync(
        Guid sessionId,
        string? input,
        CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return SessionErrors.NotFound();
        }

        var parsed = SongInputParser.Parse(input);

        ErrorOr<QueueItem> item = parsed switch
        {
            SpotifyTrackInput spotify => await CreateSpotifyItemAsync(sessionId, spotify.TrackId, cancellationToken),
            YoutubeInput youtube => await CreateYoutubeItemAsync(sessionId, youtube, cancellationToken),
            _ => Error.Validation(
                description: "Expected a YouTube URL, a Spotify track link or a spotify:track: URI")
        };

        if (item.IsError)
        {
            _logger.LogInformation(
                "Could not queue a song in session {SessionId}: {Error}",
                sessionId,
                item.FirstError.Description);
            return item.FirstError;
        }

        var saved = await _queueRepository.AddAsync(item.Value, cancellationToken);
        var dto = saved.ToDto();
        _logger.LogInformation("Queued song {SongId} ({Source}) in session {SessionId}", saved.Id, saved.Source, sessionId);
        await _musicNotifier.SongAddedAsync(sessionId, dto, cancellationToken);

        return dto;
    }

    public async Task<ErrorOr<IReadOnlyList<QueueItemDto>>> GetQueueAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (await _sessionsRepository.GetAsync(sessionId, cancellationToken) is null)
        {
            return SessionErrors.NotFound();
        }

        var items = await _queueRepository.GetPendingAsync(sessionId, cancellationToken);

        return items.Select(x => x.ToDto()).ToList();
    }

    public async Task<ErrorOr<QueueItemDto>> GetNextAsync(
        Guid sessionId,
        string hostProfileId,
        int? currentSongId,
        CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return SessionErrors.NotFound();
        }

        if (session.HostProfileId != hostProfileId)
        {
            return Error.Forbidden(description: "Only the host of this session can advance its queue");
        }

        return await AdvanceAndNotifyAsync(sessionId, currentSongId, cancellationToken);
    }

    public async Task<ErrorOr<Success>> RemoveAsync(
        Guid sessionId,
        int songId,
        SessionCaller caller,
        CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return SessionErrors.NotFound();
        }

        if (!SessionRoles.IsManager(session, caller))
        {
            return Error.Forbidden(description: "Only the host or an admin can remove songs from the queue");
        }

        var removal = await _queueRepository.RemoveAsync(sessionId, songId, cancellationToken);

        if (removal == QueueItemRemoval.Removed)
        {
            _logger.LogInformation("Removed song {SongId} from session {SessionId}", songId, sessionId);
            await _musicNotifier.SongRemovedAsync(sessionId, songId, cancellationToken);

            // Removing the song the fallback playlist queued must not leave "Up next" empty
            await _fallbackFiller.EnsureQueuedAsync(sessionId, cancellationToken);
            return Result.Success;
        }

        return removal switch
        {
            QueueItemRemoval.NotQueued => Error.Conflict(description: "The song is no longer waiting in the queue"),
            _ => Error.NotFound(description: "The song was not found in the queue")
        };
    }

    /// <summary>
    /// Advances only when <paramref name="currentSongId"/> matches the song Playing now (same rule as the
    /// repository). A real advance is pushed to listeners even when the queue then becomes empty.
    /// </summary>
    internal async Task<ErrorOr<QueueItemDto>> AdvanceAndNotifyAsync(
        Guid sessionId,
        int? currentSongId,
        CancellationToken cancellationToken)
    {
        // An empty queue gets a playlist song first (a previous refill may have failed), so the advance finds one
        await _fallbackFiller.EnsureQueuedAsync(sessionId, cancellationToken);

        var playingBefore = await _queueRepository.GetPlayingAsync(sessionId, cancellationToken);
        var next = await _queueRepository.AdvanceAsync(sessionId, currentSongId, cancellationToken);

        // Queue the playlist song that follows the one now starting, behind whatever guests add meanwhile
        await _fallbackFiller.EnsureQueuedAsync(sessionId, cancellationToken);

        if (playingBefore?.Id == currentSongId)
        {
            _logger.LogInformation(
                "Session {SessionId} advanced from song {PreviousSongId} to {NextSongId}",
                sessionId,
                playingBefore?.Id,
                next?.Id);
            await _musicNotifier.QueueAdvancedAsync(
                sessionId,
                playingBefore?.Id,
                next?.ToDto(),
                cancellationToken);
        }

        return next is null
            ? Error.NotFound(description: "The queue is empty")
            : next.ToDto();
    }


    private async Task<ErrorOr<QueueItem>> CreateYoutubeItemAsync(
        Guid sessionId,
        YoutubeInput youtube,
        CancellationToken cancellationToken)
    {
        var lookup = await _youtubeClient.GetVideoAsync(youtube.VideoId, cancellationToken);

        switch (lookup)
        {
            case { Status: YoutubeVideoStatus.NotFound }:
                return Error.NotFound(description: "YouTube video was not found");

            case { Status: YoutubeVideoStatus.NotEmbeddable }:
                return Error.Validation(
                    description: "The owner of this YouTube video does not allow it to be played outside YouTube");

            case { Status: YoutubeVideoStatus.Found, Video: { } video }:
                return new QueueItem(
                    sessionId, QueueItemSource.Youtube, youtube.Url, video.Title, video.AuthorName,
                    video.ThumbnailUrl, null);

            // YouTube could not be asked: better to queue the link than to refuse a song that may well play
            default:
                return new QueueItem(sessionId, QueueItemSource.Youtube, youtube.Url, youtube.Url, null, null, null);
        }
    }

    private async Task<ErrorOr<QueueItem>> CreateSpotifyItemAsync(
        Guid sessionId,
        string trackId,
        CancellationToken cancellationToken)
    {
        var token = await _hostTokenProvider.GetSessionAccessTokenAsync(sessionId, cancellationToken);
        if (token.IsError)
        {
            return token.FirstError;
        }

        var song = await _spotifyClient.GetTrackAsync(token.Value, trackId, cancellationToken);

        if (song is null)
        {
            return Error.NotFound(description: "Spotify track was not found");
        }

        return song.ToQueueItem(sessionId);
    }
}
