using PartyKing.Infrastructure.Spotify.Mappings;
using SpotifyAPI.Web;

namespace PartyKing.Application.Tests;

public class FullTrackMappingsTests
{
    private static FullTrack NewTrack(List<Image>? images = null) => new()
    {
        Id = "t1",
        Uri = "spotify:track:t1",
        Name = "Song",
        DurationMs = 200000,
        Album = new SimpleAlbum
        {
            Name = "Album",
            Images = images ?? [],
            Artists = [new SimpleArtist { Name = "Album Artist" }]
        },
        Artists = [new SimpleArtist { Name = "One" }, new SimpleArtist { Name = "Two" }]
    };

    [Fact]
    public void FullTrack_ToDomain_MapsAllFields()
    {
        var track = NewTrack([new Image { Url = "https://img/large" }, new Image { Url = "https://img/small" }]);

        var song = track.ToDomain();

        Assert.Equal("t1", song.Id);
        Assert.Equal("spotify:track:t1", song.Uri);
        Assert.Equal("Song", song.Name);
        Assert.Equal(200000, song.DurationMs);
        Assert.Equal("https://img/large", song.AlbumArtUrl);
        Assert.Equal(["One", "Two"], song.Artists.Select(a => a.Name));
        Assert.Equal("One, Two", song.ArtistsDisplay);
        Assert.Equal("Album", song.Album.Name);
        Assert.Equal(["Album Artist"], song.Album.Artists.Select(a => a.Name));
    }

    [Fact]
    public void FullTrack_ToDomain_WithoutImages_HasNoAlbumArt()
    {
        Assert.Null(NewTrack().ToDomain().AlbumArtUrl);
    }

    [Fact]
    public void FullPlaylist_ToDomain_MapsAllFields()
    {
        var playlist = new FullPlaylist
        {
            Id = "p1",
            Name = "Mix",
            Owner = new PublicUser { Id = "owner" },
            Images = [new Image { Url = "https://img/p" }],
            Tracks = new Paging<PlaylistTrack<IPlayableItem>> { Total = 42 }
        };

        var result = playlist.ToDomain();

        Assert.Equal("p1", result.Id);
        Assert.Equal("Mix", result.Name);
        Assert.Equal("owner", result.OwnerId);
        Assert.Equal("https://img/p", result.ImageUrl);
        Assert.Equal(42, result.TrackCount);
    }

    [Fact]
    public void FullPlaylist_ToDomain_MissingParts_UseFallbacks()
    {
        var playlist = new FullPlaylist { Id = "p1" };

        var result = playlist.ToDomain();

        Assert.Equal(string.Empty, result.Name);
        Assert.Equal(string.Empty, result.OwnerId);
        Assert.Null(result.ImageUrl);
        Assert.Equal(0, result.TrackCount);
    }
}
