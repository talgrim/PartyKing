using PartyKing.Application.Mappings;
using PartyKing.Contract.V1.Enums;
using PartyKing.Contract.V1.Slideshow;
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
        var session = new Session("ABC123", "Party", "hash");

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
    public void Session_ToDto_WithHostProfile_UsesDisplayName()
    {
        var profile = new SpotifyProfile("profile-id", "refresh");
        profile.UpdateDisplayName("Display Name");
        var session = new Session("ABC123", "Party", "hash")
        {
            HostProfileId = "profile-id",
            HostProfile = profile,
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
        var session = new Session("ABC123", "Party", "hash") { HostProfileId = "profile-id" };

        var dto = session.ToDto();

        Assert.Equal("profile-id", dto.HostName);
        Assert.True(dto.HasHost);
    }

    [Fact]
    public void Session_ToDto_ProfileWithoutDisplayName_FallsBackToProfileId()
    {
        var session = new Session("ABC123", "Party", "hash")
        {
            HostProfileId = "profile-id",
            HostProfile = new SpotifyProfile("profile-id", "refresh")
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
        var settings = new SlideshowSettings
        {
            SessionId = Guid.NewGuid(),
            AutoRepeat = true,
            StartFromBeginning = false,
            SlideTime = TimeSpan.FromSeconds(12)
        };

        var dto = settings.ToDto();

        Assert.True(dto.AutoRepeat);
        Assert.False(dto.StartFromBeginning);
        Assert.Equal(TimeSpan.FromSeconds(12), dto.SlideTime);
    }

    [Fact]
    public void SlideshowSettingsDto_ToDomain_UsesGivenSlideTime()
    {
        var sessionId = Guid.NewGuid();
        var dto = new SlideshowSettingsDto
        {
            AutoRepeat = false,
            StartFromBeginning = true,
            SlideTime = TimeSpan.FromSeconds(8)
        };

        var settings = dto.ToDomain(sessionId, TimeSpan.FromSeconds(30));

        Assert.Equal(sessionId, settings.SessionId);
        Assert.False(settings.AutoRepeat);
        Assert.True(settings.StartFromBeginning);
        Assert.Equal(TimeSpan.FromSeconds(8), settings.SlideTime);
    }

    [Fact]
    public void SlideshowSettingsDto_ToDomain_WithoutSlideTime_UsesDefault()
    {
        var dto = new SlideshowSettingsDto { AutoRepeat = true, StartFromBeginning = true };

        var settings = dto.ToDomain(Guid.NewGuid(), TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(30), settings.SlideTime);
    }
}
