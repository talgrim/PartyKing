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
    Task<ErrorOr<SongDataDto>> EnqueueAsync(string? input, CancellationToken cancellationToken);
    Task<IReadOnlyList<SongDataDto>> GetQueueAsync(CancellationToken cancellationToken);
    Task<ErrorOr<SongDataDto>> GetNextAsync(CancellationToken cancellationToken);
}

internal class QueueService : IQueueService
{
    private readonly IQueueRepository _queueRepository;
    private readonly ISpotifyIntegrationClient _spotifyClient;
    private readonly IHostTokenProvider _hostTokenProvider;

    public QueueService(
        IQueueRepository queueRepository,
        ISpotifyIntegrationClient spotifyClient,
        IHostTokenProvider hostTokenProvider)
    {
        _queueRepository = queueRepository;
        _spotifyClient = spotifyClient;
        _hostTokenProvider = hostTokenProvider;
    }

    public async Task<ErrorOr<SongDataDto>> EnqueueAsync(string? input, CancellationToken cancellationToken)
    {
        var parsed = SongInputParser.Parse(input);

        ErrorOr<QueueItem> item = parsed switch
        {
            SpotifyTrackInput spotify => await CreateSpotifyItemAsync(spotify.TrackId, cancellationToken),
            YoutubeInput youtube => new QueueItem(QueueItemSource.Youtube, youtube.Url, youtube.Url, null, null, null),
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

    public async Task<IReadOnlyList<SongDataDto>> GetQueueAsync(CancellationToken cancellationToken)
    {
        var items = await _queueRepository.GetPendingAsync(cancellationToken);

        return items.Select(x => x.ToDto()).ToList();
    }

    public async Task<ErrorOr<SongDataDto>> GetNextAsync(CancellationToken cancellationToken)
    {
        var next = await _queueRepository.AdvanceAsync(cancellationToken);

        if (next is null)
        {
            return Error.NotFound(description: "The queue is empty");
        }

        return next.ToDto();
    }

    private async Task<ErrorOr<QueueItem>> CreateSpotifyItemAsync(string trackId, CancellationToken cancellationToken)
    {
        var token = await _hostTokenProvider.GetAccessTokenAsync(cancellationToken);
        if (token.IsError)
        {
            return token.FirstError;
        }

        var song = await _spotifyClient.GetTrackAsync(token.Value, trackId, cancellationToken);

        if (song is null)
        {
            return Error.NotFound(description: "Spotify track was not found");
        }

        return song.ToQueueItem();
    }
}
