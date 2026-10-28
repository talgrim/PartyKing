using PartyKing.Contract.V1.Enums;
using PartyKing.Contract.V1.Queue;
using PartyKing.Contract.V1.Spotify;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Mappings;

public static class QueueMappings
{
    public static QueueItemDto ToDto(this QueueItem item)
    {
        var source = item.Source == QueueItemSource.Spotify ? SongSource.Spotify : SongSource.Youtube;

        return new QueueItemDto(
            item.Id,
            source,
            item.Url,
            item.Title,
            item.Artists,
            item.AlbumArtUrl,
            item.DurationMs,
            item.IsFallback);
    }

    public static QueueItem ToQueueItem(this Song song, Guid sessionId, bool isFallback = false)
    {
        return new QueueItem(
            sessionId,
            QueueItemSource.Spotify,
            song.Uri,
            song.Name,
            song.ArtistsDisplay,
            song.AlbumArtUrl,
            song.DurationMs)
        {
            IsFallback = isFallback
        };
    }

    public static SpotifyTrackDto ToDto(this Song song)
    {
        return new SpotifyTrackDto(song.Uri, song.Name, song.ArtistsDisplay, song.AlbumArtUrl, song.DurationMs);
    }
}
