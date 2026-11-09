using PartyKing.Application.Mappings;
using PartyKing.Contract.V1.Enums;
using PartyKing.Domain.Entities.Playlists;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Entities.Slideshow;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Tests;

public class ApplicationMappingsTests
{
    private static Song NewSong() => new()
    {
        Id = "id1",
        Uri = "spotify:track:id1",
        Name = "Title",
        Artists = [new Artist { Name = "A" }, new Artist { Name = "B" }],
        Album = new Album { Name = "Album", Artists = [] },
        AlbumArtUrl = "https://img/1.jpg",
        DurationMs = 123456
    };

    [Fact]
    public void Session_ToDto_WithoutHost_HasNoHost()
    {
        var session = new Session(Guid.NewGuid(), "ABC123", "Party", "hash", DateTime.UtcNow);

        var dto = session.ToDto();

        Assert.Equal(session.Id, dto.Id);
        Assert.Equal("ABC123", dto.Code);
        Assert.Equal("Party", dto.Name);
        Assert.Null(dto.HostName);
        Assert.False(dto.HasHost);
        Assert.Null(dto.FallbackPlaylistName);
        Assert.False(dto.FallbackShuffle);
    }

    [Fact]
    public void Session_ToDto_WithASavedPlaylistFallback_CarriesItsIdAndName()
    {
        var session = new Session(Guid.NewGuid(), "ABC123", "Party", "hash", DateTime.UtcNow);
        var savedId = Guid.NewGuid();
        session.SetFallbackSavedPlaylist(savedId, "Mine", shuffle: true);

        var dto = session.ToDto();

        Assert.Equal(savedId, dto.FallbackSavedPlaylistId);
        Assert.Equal("Mine", dto.FallbackPlaylistName);
        Assert.True(dto.FallbackShuffle);
    }

    [Fact]
    public void SavedPlaylistSummary_ToDto_MapsEveryField()
    {
        var summary = new SavedPlaylistSummary(Guid.NewGuid(), "Friday", new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc), 7);

        var dto = summary.ToDto();

        Assert.Equal((summary.Id, "Friday", summary.CreatedAt, 7), (dto.Id, dto.Name, dto.CreatedAt, dto.SongCount));
    }

    [Theory]
    [InlineData(QueueItemSource.Youtube, true)]
    [InlineData(QueueItemSource.Spotify, false)]
    public void SavedPlaylistItem_ToQueueItem_KeepsTheSongAndItsSource(QueueItemSource source, bool isFallback)
    {
        var sessionId = Guid.NewGuid();
        var queue = new QueueItem(Guid.NewGuid(), source, "the-url", "Title", "Artists", "https://art", 4000) { Id = 1 };
        var item = SavedPlaylist.Create(Guid.NewGuid(), Guid.NewGuid(), "Mix", [queue], 0, DateTime.UtcNow).Playlist!.Items.Single();

        var mapped = item.ToQueueItem(sessionId, isFallback);

        Assert.Equal((sessionId, source, "the-url", "Title", "Artists", "https://art", 4000), (mapped.SessionId, mapped.Source, mapped.Url, mapped.Title, mapped.Artists, mapped.AlbumArtUrl, mapped.DurationMs));
        Assert.Equal(QueueItemStatus.Queued, mapped.Status);
        Assert.Equal(isFallback, mapped.IsFallback);
    }

    [Fact]
    public void Session_ToDto_WithHostProfile_UsesDisplayName()
    {
        var session = new Session(Guid.NewGuid(), "ABC123", "Party", "hash", DateTime.UtcNow)
        {
            HostProfileId = "profile-id",
            HostDisplayName = "Display Name",
            FallbackPlaylistName = "Playlist",
            FallbackShuffle = true
        };

        var dto = session.ToDto();

        Assert.Equal("Display Name", dto.HostName);
        Assert.True(dto.HasHost);
        Assert.Equal("Playlist", dto.FallbackPlaylistName);
        Assert.True(dto.FallbackShuffle);
    }

    [Fact]
    public void Session_ToDto_HostProfileNotLoaded_FallsBackToProfileId()
    {
        var session = new Session(Guid.NewGuid(), "ABC123", "Party", "hash", DateTime.UtcNow) { HostProfileId = "profile-id" };

        var dto = session.ToDto();

        Assert.Equal("profile-id", dto.HostName);
        Assert.True(dto.HasHost);
    }

    [Fact]
    public void Session_ToDto_ProfileWithoutDisplayName_FallsBackToProfileId()
    {
        var session = new Session(Guid.NewGuid(), "ABC123", "Party", "hash", DateTime.UtcNow)
        {
            HostProfileId = "profile-id"
        };

        Assert.Equal("profile-id", session.ToDto().HostName);
    }

    [Theory]
    [InlineData(QueueItemSource.Spotify, SongSource.Spotify)]
    [InlineData(QueueItemSource.Youtube, SongSource.Youtube)]
    public void QueueItem_ToDto_MapsAllFields(QueueItemSource source, SongSource expected)
    {
        var item = new QueueItem(Guid.NewGuid(), source, "url", "Title", "Artists", "https://art", 1000)
        {
            Id = 7,
            IsFallback = true
        };

        var dto = item.ToDto();

        Assert.Equal(7, dto.Id);
        Assert.Equal(expected, dto.Source);
        Assert.Equal("url", dto.Url);
        Assert.Equal("Title", dto.Name);
        Assert.Equal("Artists", dto.Artists);
        Assert.Equal("https://art", dto.AlbumArtUrl);
        Assert.Equal(1000, dto.DurationMs);
        Assert.True(dto.IsFallback);
    }

    [Fact]
    public void QueueItem_ToDto_NullableFieldsStayNull()
    {
        var item = new QueueItem(Guid.NewGuid(), QueueItemSource.Youtube, "url", "Title", null, null, null);

        var dto = item.ToDto();

        Assert.Null(dto.Artists);
        Assert.Null(dto.AlbumArtUrl);
        Assert.Null(dto.DurationMs);
        Assert.False(dto.IsFallback);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Song_ToQueueItem_MapsSongAndContext(bool isFallback)
    {
        var sessionId = Guid.NewGuid();

        var item = NewSong().ToQueueItem(sessionId, isFallback);

        Assert.Equal(sessionId, item.SessionId);
        Assert.Equal(QueueItemSource.Spotify, item.Source);
        Assert.Equal("spotify:track:id1", item.Url);
        Assert.Equal("Title", item.Title);
        Assert.Equal("A, B", item.Artists);
        Assert.Equal("https://img/1.jpg", item.AlbumArtUrl);
        Assert.Equal(123456, item.DurationMs);
        Assert.Equal(QueueItemStatus.Queued, item.Status);
        Assert.Equal(isFallback, item.IsFallback);
    }

    [Fact]
    public void Song_ToQueueItem_DefaultsToNotFallback()
    {
        Assert.False(NewSong().ToQueueItem(Guid.NewGuid()).IsFallback);
    }

    [Fact]
    public void Song_ToDto_MapsAllFields()
    {
        var dto = NewSong().ToDto();

        Assert.Equal("spotify:track:id1", dto.Uri);
        Assert.Equal("Title", dto.Name);
        Assert.Equal("A, B", dto.Artists);
        Assert.Equal("https://img/1.jpg", dto.AlbumArtUrl);
        Assert.Equal(123456, dto.DurationMs);
    }

    [Fact]
    public void SlideshowSettings_ToDto_MapsAllFields()
    {
        var settings = new SlideshowSettings(Guid.NewGuid(), true, TimeSpan.FromSeconds(12), false);

        var dto = settings.ToDto();

        Assert.True(dto.AutoRepeat);
        Assert.False(dto.StartFromBeginning);
        Assert.Equal(TimeSpan.FromSeconds(12), dto.SlideTime);
    }
}
