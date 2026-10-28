using PartyKing.Domain.Entities.Spotify;
using SpotifyAPI.Web;

namespace PartyKing.Infrastructure.Spotify.Mappings;

public static class FullTrackMappings
{
    public static Song ToDomain(this FullTrack track)
    {
        return new Song
        {
            Id = track.Id,
            Uri = track.Uri,
            Name = track.Name,
            AlbumArtUrl = track.Album.Images.FirstOrDefault()?.Url,
            DurationMs = track.DurationMs,
            Album = track.Album.ToDomain(),
            Artists = track.Artists.Select(ToDomain).ToList(),
        };
    }

    public static Playlist ToDomain(this FullPlaylist playlist)
    {
        return new Playlist
        {
            Id = playlist.Id!,
            Name = playlist.Name ?? string.Empty,
            OwnerId = playlist.Owner?.Id ?? string.Empty,
            ImageUrl = playlist.Images?.FirstOrDefault()?.Url,
            TrackCount = playlist.Tracks?.Total ?? 0
        };
    }

    public static Album ToDomain(this SimpleAlbum album)
    {
        return new Album
        {
            Name = album.Name,
            Artists = album.Artists.Select(ToDomain).ToList()
        };
    }

    public static Artist ToDomain(this SimpleArtist artist)
    {
        return new Artist
        {
            Name = artist.Name
        };
    }
}