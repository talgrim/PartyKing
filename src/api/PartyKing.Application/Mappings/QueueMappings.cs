using PartyKing.Contract.V1.Queue;
using PartyKing.Contract.V1.Spotify;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Enums;
using Riok.Mapperly.Abstractions;

namespace PartyKing.Application.Mappings;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.Target,
    EnumMappingStrategy = EnumMappingStrategy.ByName)]
public static partial class QueueMappings
{
    [MapProperty(nameof(QueueItem.Title), nameof(QueueItemDto.Name))]
    public static partial QueueItemDto ToDto(this QueueItem item);

    // Id and Status are not copied from the song: the database assigns the id and the constructor starts at Queued.
    [MapperIgnoreTarget(nameof(QueueItem.Id))]
    [MapperIgnoreTarget(nameof(QueueItem.Status))]
    [MapValue(nameof(QueueItem.Source), QueueItemSource.Spotify)]
    [MapProperty(nameof(Song.Uri), nameof(QueueItem.Url))]
    [MapProperty(nameof(Song.Name), nameof(QueueItem.Title))]
    [MapProperty(nameof(Song.ArtistsDisplay), nameof(QueueItem.Artists))]
    public static partial QueueItem ToQueueItem(this Song song, Guid sessionId, bool isFallback = false);

    [MapProperty(nameof(Song.ArtistsDisplay), nameof(SpotifyTrackDto.Artists))]
    public static partial SpotifyTrackDto ToDto(this Song song);
}
