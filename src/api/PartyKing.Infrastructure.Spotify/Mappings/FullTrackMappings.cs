using PartyKing.Domain.Entities.Spotify;
using Riok.Mapperly.Abstractions;
using SpotifyAPI.Web;

namespace PartyKing.Infrastructure.Spotify.Mappings;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class FullTrackMappings
{
    [MapPropertyFromSource(nameof(Song.AlbumArtUrl), Use = nameof(AlbumArtOf))]
    public static partial Song ToDomain(this FullTrack track);

    [MapPropertyFromSource(nameof(Playlist.Id), Use = nameof(PlaylistIdOf))]
    [MapPropertyFromSource(nameof(Playlist.Name), Use = nameof(PlaylistNameOf))]
    [MapPropertyFromSource(nameof(Playlist.OwnerId), Use = nameof(OwnerIdOf))]
    [MapPropertyFromSource(nameof(Playlist.ImageUrl), Use = nameof(PlaylistImageOf))]
    [MapPropertyFromSource(nameof(Playlist.TrackCount), Use = nameof(TrackCountOf))]
    public static partial Playlist ToDomain(this FullPlaylist playlist);

    public static partial Album ToDomain(this SimpleAlbum album);

    public static partial Artist ToDomain(this SimpleArtist artist);

    private static string? AlbumArtOf(FullTrack track) => track.Album.Images.FirstOrDefault()?.Url;

    private static string PlaylistIdOf(FullPlaylist playlist) => playlist.Id!;

    private static string PlaylistNameOf(FullPlaylist playlist) => playlist.Name ?? string.Empty;

    private static string OwnerIdOf(FullPlaylist playlist) => playlist.Owner?.Id ?? string.Empty;

    private static string? PlaylistImageOf(FullPlaylist playlist) => playlist.Images?.FirstOrDefault()?.Url;

    private static int TrackCountOf(FullPlaylist playlist) => playlist.Tracks?.Total ?? 0;
}
