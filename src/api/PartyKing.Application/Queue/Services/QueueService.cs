using PartyKing.Application.Ports;
using PartyKing.Domain.Repositories;
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
    private readonly IQueueAdvancer _queueAdvancer;
    private readonly ILogger<QueueService> _logger;

    public QueueService(
        IQueueRepository queueRepository,
        ISessionsRepository sessionsRepository,
        ISpotifyIntegrationClient spotifyClient,
        IYoutubeOEmbedClient youtubeClient,
        IHostTokenProvider hostTokenProvider,
        IQueueAdvancer queueAdvancer,
        ILogger<QueueService> logger)
    {
        _queueRepository = queueRepository;
        _sessionsRepository = sessionsRepository;
        _spotifyClient = spotifyClient;
        _youtubeClient = youtubeClient;
        _hostTokenProvider = hostTokenProvider;
        _queueAdvancer = queueAdvancer;
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

        return await _queueAdvancer.AdvanceAsync(sessionId, currentSongId, cancellationToken);
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

            return Result.Success;
        }

        return removal switch
        {
            QueueItemRemoval.NotQueued => Error.Conflict(description: "The song is no longer waiting in the queue"),
            _ => Error.NotFound(description: "The song was not found in the queue")
        };
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
