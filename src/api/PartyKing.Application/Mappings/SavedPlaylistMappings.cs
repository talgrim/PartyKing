using PartyKing.Contract.V1.Enums;
using PartyKing.Contract.V1.Playlists;
using PartyKing.Domain.Entities.Playlists;
using Riok.Mapperly.Abstractions;

namespace PartyKing.Application.Mappings;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.Target,
    EnumMappingStrategy = EnumMappingStrategy.ByName)]
public static partial class SavedPlaylistMappings
{
    public static partial QueueSection ToDomain(this PlaylistSection section);

    public static partial PlaylistSection ToDto(this QueueSection section);

    public static partial PlaylistSectionCountDto ToDto(this QueueSectionCount count);

    [MapProperty(nameof(SavedPlaylist.Items) + "." + nameof(SavedPlaylist.Items.Count), nameof(SavedPlaylistDto.SongCount))]
    public static partial SavedPlaylistDto ToDto(this SavedPlaylist playlist);
}
