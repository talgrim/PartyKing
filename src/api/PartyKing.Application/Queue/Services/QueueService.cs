using ErrorOr;
using PartyKing.Application.Mappings;
using PartyKing.Application.Queue.Helpers;
using PartyKing.Application.Spotify.Services;
using PartyKing.Contract.V1.Queue;
using PartyKing.Domain.Entities;
using PartyKing.Domain.Enums;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Spotify.RestApi;

namespace PartyKing.Application.Queue.Services;

public interface IQueueService
{
    Task<ErrorOr<SongDataDto>> EnqueueAsync(Guid sessionId, string? input, CancellationToken cancellationToken);
    Task<ErrorOr<IReadOnlyList<SongDataDto>>> GetQueueAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Host only: forbidden unless <paramref name="hostProfileId"/> hosts the session.</summary>
    Task<ErrorOr<SongDataDto>> GetNextAsync(Guid sessionId, string hostProfileId, CancellationToken cancellationToken);
}

internal class QueueService : IQueueService
{
    private readonly IQueueRepository _queueRepository;
    private readonly ISpotifyIntegrationClient _spotifyClient;
    private readonly IHostTokenProvider _hostTokenProvider;
    private readonly ISessionsRepository _sessionsRepository;

    public QueueService(
        IQueueRepository queueRepository,
        ISessionsRepository sessionsRepository,
        ISpotifyIntegrationClient spotifyClient,
        IHostTokenProvider hostTokenProvider)
    {
        _queueRepository = queueRepository;
        _sessionsRepository = sessionsRepository;
        _spotifyClient = spotifyClient;
        _hostTokenProvider = hostTokenProvider;
    }

    public async Task<ErrorOr<SongDataDto>> EnqueueAsync(
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
            YoutubeInput youtube => new QueueItem(
                sessionId, QueueItemSource.Youtube, youtube.Url, youtube.Url, null, null, null),
            _ => Error.Validation(
                description: "Expected a YouTube URL, a Spotify track link or a spotify:track: URI")
        };

        if (item.IsError)
        {
            return item.FirstError;
        }

        var saved = await _queueRepository.AddAsync(item.Value, cancellationToken);

        return saved.ToDto();
    }

    public async Task<ErrorOr<IReadOnlyList<SongDataDto>>> GetQueueAsync(
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

    public async Task<ErrorOr<SongDataDto>> GetNextAsync(
        Guid sessionId,
        string hostProfileId,
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

        var next = await _queueRepository.AdvanceAsync(sessionId, cancellationToken);

        if (next is null)
        {
            return Error.NotFound(description: "The queue is empty");
        }

        return next.ToDto();
    }

    private static Error SessionNotFound() => Error.NotFound(description: "The session was not found");

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
