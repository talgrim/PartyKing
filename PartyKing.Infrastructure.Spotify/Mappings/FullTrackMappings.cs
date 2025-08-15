using PartyKing.Domain.Models.Spotify;
using SpotifyAPI.Web;

namespace PartyKing.Infrastructure.Spotify.Mappings;

public static class FullTrackMappings
{
    public static Song ToDomain(this FullTrack track)
    {
        return new Song
        {
            Name = track.Name,
            Album = track.Album.ToDomain(),
            Artists = track.Artists.Select(ToDomain).ToList(),
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