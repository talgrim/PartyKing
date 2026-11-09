using PartyKing.Domain.Entities.Playlists;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Enums;

namespace PartyKing.Domain.Tests;

public class SavedPlaylistTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static QueueItem Song(int id)
    {
        return new QueueItem(
            Guid.NewGuid(),
            id % 2 == 0 ? QueueItemSource.Youtube : QueueItemSource.Spotify,
            $"url-{id}",
            $"Song {id}",
            $"Artist {id}",
            $"https://art/{id}",
            id * 1000)
        {
            Id = id
        };
    }

    private static SavedPlaylistCreation Create(
        string? name = "Friday",
        int songs = 2,
        int ownerPlaylistCount = 0)
    {
        return SavedPlaylist.Create(
            Guid.NewGuid(),
            OwnerId,
            name,
            Enumerable.Range(1, songs).Select(Song).ToList(),
            ownerPlaylistCount,
            Now);
    }

    [Fact]
    public void Create_TakesTheIdTheOwnerAndTheTimeItIsGiven_AndTrimsTheName()
    {
        var id = Guid.NewGuid();

        var creation = SavedPlaylist.Create(id, OwnerId, "  Friday  ", [Song(1)], 0, Now);

        Assert.Equal(SavedPlaylistFailure.None, creation.Failure);
        Assert.Equal(id, creation.Playlist!.Id);
        Assert.Equal(OwnerId, creation.Playlist.OwnerAccountId);
        Assert.Equal("Friday", creation.Playlist.Name);
        Assert.Equal(Now, creation.Playlist.CreatedAt);
    }

    [Fact]
    public void Create_CopiesTheSongsInTheOrderGiven_WithTheirPosition()
    {
        var playlist = Create(songs: 3).Playlist!;

        Assert.Equal([0, 1, 2], playlist.Items.Select(x => x.Position));
        Assert.Equal(["url-1", "url-2", "url-3"], playlist.Items.Select(x => x.Url));
    }

    [Fact]
    public void Create_CopiesEveryFieldOfASong()
    {
        var item = Create(songs: 2).Playlist!.Items[1];

        Assert.Equal(QueueItemSource.Youtube, item.Source);
        Assert.Equal("url-2", item.Url);
        Assert.Equal("Song 2", item.Title);
        Assert.Equal("Artist 2", item.Artists);
        Assert.Equal("https://art/2", item.AlbumArtUrl);
        Assert.Equal(2000, item.DurationMs);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankName_IsRefused(string? name)
    {
        var creation = Create(name);

        Assert.Equal(SavedPlaylistFailure.NameEmpty, creation.Failure);
        Assert.Null(creation.Playlist);
    }

    [Fact]
    public void ANameOfTheMaximumLength_IsAccepted_AndOneCharacterMoreIsTooLong()
    {
        Assert.Equal(SavedPlaylistFailure.None, Create(new string('a', SavedPlaylist.MaxNameLength)).Failure);
        Assert.Equal(
            SavedPlaylistFailure.NameTooLong,
            Create(new string('a', SavedPlaylist.MaxNameLength + 1)).Failure);
    }

    [Fact]
    public void TheLengthOfTheName_IsCheckedAfterTrimming()
    {
        var name = $"  {new string('a', SavedPlaylist.MaxNameLength)}  ";

        Assert.Equal(SavedPlaylistFailure.None, Create(name).Failure);
    }

    [Fact]
    public void WithoutSongs_NoPlaylistIsMade()
    {
        var creation = Create(songs: 0);

        Assert.Equal(SavedPlaylistFailure.NoSongs, creation.Failure);
        Assert.Null(creation.Playlist);
    }

    [Fact]
    public void TheMaximumNumberOfSongs_IsAccepted_AndOneMoreIsTooMany()
    {
        Assert.Equal(SavedPlaylistFailure.None, Create(songs: SavedPlaylist.MaxSongs).Failure);
        Assert.Equal(SavedPlaylistFailure.TooManySongs, Create(songs: SavedPlaylist.MaxSongs + 1).Failure);
    }

    [Fact]
    public void AnOwnerWithTheMaximumNumberOfPlaylists_CannotSaveAnother()
    {
        Assert.Equal(
            SavedPlaylistFailure.None,
            Create(ownerPlaylistCount: SavedPlaylist.MaxPerAccount - 1).Failure);
        Assert.Equal(
            SavedPlaylistFailure.TooManyPlaylists,
            Create(ownerPlaylistCount: SavedPlaylist.MaxPerAccount).Failure);
    }

    [Fact]
    public void TheNameIsChecked_BeforeTheSongs()
    {
        Assert.Equal(SavedPlaylistFailure.NameEmpty, Create(name: " ", songs: 0).Failure);
    }
}
