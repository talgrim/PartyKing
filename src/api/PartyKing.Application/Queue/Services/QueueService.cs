using ErrorOr;
using PartyKing.Application.Mappings;
using PartyKing.Application.Queue.Helpers;
using PartyKing.Application.Realtime;
using PartyKing.Application.Spotify.Services;
using PartyKing.Contract.V1.Queue;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Repositories;
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
    /// Removes a waiting song. Forbidden unless the caller is an admin or <paramref name="profileId"/> hosts the
    /// session; conflict when the song is no longer waiting (it is playing or has been played).
    /// </summary>
    Task<ErrorOr<Success>> RemoveAsync(
        Guid sessionId,
        int songId,
        string? profileId,
        bool isAdmin,
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

    public QueueService(
        IQueueRepository queueRepository,
        ISessionsRepository sessionsRepository,
        ISpotifyIntegrationClient spotifyClient,
        IYoutubeOEmbedClient youtubeClient,
        IHostTokenProvider hostTokenProvider,
        ISessionMusicNotifier musicNotifier)
    {
        _queueRepository = queueRepository;
        _sessionsRepository = sessionsRepository;
        _spotifyClient = spotifyClient;
        _youtubeClient = youtubeClient;
        _hostTokenProvider = hostTokenProvider;
        _musicNotifier = musicNotifier;
    }

    public async Task<ErrorOr<QueueItemDto>> EnqueueAsync(
        Guid sessionId,
        string? input,
        CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return SessionNotFound();
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
            return item.FirstError;
        }

        var saved = await _queueRepository.AddAsync(item.Value, cancellationToken);
        var dto = saved.ToDto();
        await _musicNotifier.SongAddedAsync(sessionId, dto, cancellationToken);

        return dto;
    }

    public async Task<ErrorOr<IReadOnlyList<QueueItemDto>>> GetQueueAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (await _sessionsRepository.GetAsync(sessionId, cancellationToken) is null)
        {
            return SessionNotFound();
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
            return SessionNotFound();
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
        string? profileId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return SessionNotFound();
        }

        var isHost = profileId is not null && session.HostProfileId == profileId;
        if (!isAdmin && !isHost)
        {
            return Error.Forbidden(description: "Only the host or an admin can remove songs from the queue");
        }

        var removal = await _queueRepository.RemoveAsync(sessionId, songId, cancellationToken);

        if (removal == QueueItemRemoval.Removed)
        {
            await _musicNotifier.SongRemovedAsync(sessionId, songId, cancellationToken);
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
        var playingBefore = await _queueRepository.GetPlayingAsync(sessionId, cancellationToken);
        var next = await _queueRepository.AdvanceAsync(sessionId, currentSongId, cancellationToken);

        if (playingBefore?.Id == currentSongId)
        {
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

    private static Error SessionNotFound() => Error.NotFound(description: "The session was not found");

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
