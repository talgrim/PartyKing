using PartyKing.Contract.V1.Enums;
using PartyKing.Contract.V1.Queue;
using PartyKing.Contract.V1.Spotify;
using PartyKing.Domain.Entities;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Models.Spotify;

namespace PartyKing.Application.Mappings;

public static class QueueMappings
{
    public static SongDataDto ToDto(this QueueItem item)
    {
        var source = item.Source == QueueItemSource.Spotify ? SongSource.Spotify : SongSource.Youtube;

        return new SongDataDto(
            item.Id,
            source,
            item.Url,
            item.Title,
            item.Artists,
            item.AlbumArtUrl,
            item.DurationMs);
    }

    public static QueueItem ToQueueItem(this Song song)
    {
        return new QueueItem(
            QueueItemSource.Spotify,
            song.Uri,
            song.Name,
            song.ArtistsDisplay,
            song.AlbumArtUrl,
            song.DurationMs);
    }

    public static SpotifyTrackDto ToDto(this Song song)
    {
        return new SpotifyTrackDto(song.Uri, song.Name, song.ArtistsDisplay, song.AlbumArtUrl, song.DurationMs);
    }
}
