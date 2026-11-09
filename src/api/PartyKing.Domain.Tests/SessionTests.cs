using PartyKing.Domain.Entities.Playlists;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Events.Sessions;

namespace PartyKing.Domain.Tests;

public class SessionTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static Session NewSession()
    {
        return Session.Create(Guid.NewGuid(), "ABC123", "Party", "hash", Now).Session!;
    }

    [Theory]
    [InlineData(null, SessionNameFailure.Empty)]
    [InlineData("", SessionNameFailure.Empty)]
    [InlineData("   ", SessionNameFailure.Empty)]
    [InlineData("Party", SessionNameFailure.None)]
    public void Names_AreValidatedAfterTrimming(string? name, SessionNameFailure expected)
    {
        Assert.Equal(expected, SessionNames.Validate(name));
    }

    [Fact]
    public void ANameOfExactlyTheMaximumLength_IsValid_EvenWithSpacesAround()
    {
        var name = $"  {new string('a', SessionNames.MaxLength)}  ";

        Assert.Equal(SessionNameFailure.None, SessionNames.Validate(name));
    }

    [Fact]
    public void ANameOneCharacterOverTheMaximum_IsTooLong()
    {
        Assert.Equal(SessionNameFailure.TooLong, SessionNames.Validate(new string('a', SessionNames.MaxLength + 1)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void ABlankNameFallsBackToTheDefault(string? name)
    {
        Assert.Equal("New session", SessionNames.Normalize(name));
        Assert.Equal("New session", Session.Create(Guid.NewGuid(), "ABC123", name, "hash", Now).Session!.Name);
    }

    [Fact]
    public void Create_TakesTheIdAndTheTimeItIsGiven_AndTrimsTheName()
    {
        var id = Guid.NewGuid();

        var creation = Session.Create(id, "ABC123", "  Friday  ", "hash", Now);

        Assert.Equal(SessionNameFailure.None, creation.Failure);
        Assert.Equal((id, "ABC123", "Friday", "hash", Now), (creation.Session!.Id, creation.Session.Code, creation.Session.Name, creation.Session.PasswordHash, creation.Session.CreatedAt));
    }

    [Fact]
    public void Create_RefusesANameOverTheMaximum_AndMakesNoSession()
    {
        var creation = Session.Create(Guid.NewGuid(), "ABC123", new string('a', SessionNames.MaxLength + 1), "hash", Now);

        Assert.Equal(SessionNameFailure.TooLong, creation.Failure);
        Assert.Null(creation.Session);
    }

    [Fact]
    public void Create_AcceptsANameOfExactlyTheMaximum_EvenWithSpacesAround()
    {
        var name = $"  {new string('a', SessionNames.MaxLength)}  ";

        var creation = Session.Create(Guid.NewGuid(), "ABC123", name, "hash", Now);

        Assert.Equal(new string('a', SessionNames.MaxLength), creation.Session!.Name);
    }

    [Fact]
    public void ANewSession_RaisesNothing()
    {
        Assert.Empty(NewSession().DrainEvents());
    }

    [Fact]
    public void Rename_TrimsTheNameAndRaisesSessionRenamed()
    {
        var session = NewSession();

        var failure = session.Rename("  Friday  ");

        Assert.Equal(SessionNameFailure.None, failure);
        Assert.Equal("Friday", session.Name);
        Assert.Equal(new SessionRenamed(session.Id, "Friday"), Assert.Single(session.DrainEvents()));
    }

    [Theory]
    [InlineData(null, SessionNameFailure.Empty)]
    [InlineData(" ", SessionNameFailure.Empty)]
    [InlineData("over", SessionNameFailure.TooLong)]
    public void Rename_RefusesABadName_ChangesNothing_AndRaisesNothing(string? name, SessionNameFailure expected)
    {
        var session = NewSession();
        var badName = name == "over" ? new string('a', SessionNames.MaxLength + 1) : name;

        var failure = session.Rename(badName);

        Assert.Equal(expected, failure);
        Assert.Equal("Party", session.Name);
        Assert.Empty(session.DrainEvents());
    }

    [Fact]
    public void ChangePassword_ReplacesTheHashAndRaisesAnEventWithoutIt()
    {
        var session = NewSession();

        session.ChangePassword("new-hash");

        Assert.Equal("new-hash", session.PasswordHash);
        Assert.Equal(new SessionPasswordChanged(session.Id), Assert.Single(session.DrainEvents()));
    }

    [Fact]
    public void SetFallbackPlaylist_RewindsItAndNamesThePreviousOne()
    {
        var session = NewSession();
        session.SetFallbackPlaylist("first", "First", shuffle: false);
        session.FallbackCursor = 4;
        session.DrainEvents();

        session.SetFallbackPlaylist("second", "Second", shuffle: true);

        Assert.Equal(("second", "Second", true, 0), (session.FallbackPlaylistId, session.FallbackPlaylistName, session.FallbackShuffle, session.FallbackCursor));
        Assert.Equal(new FallbackPlaylistChanged(session.Id, FallbackPlaylistKind.Spotify, "second", "first"), Assert.Single(session.DrainEvents()));
    }

    [Fact]
    public void ClearFallbackPlaylist_RaisesOnlyWhenThereWasOne()
    {
        var session = NewSession();

        session.ClearFallbackPlaylist();
        Assert.Empty(session.DrainEvents());

        session.SetFallbackPlaylist("first", "First", shuffle: true);
        session.DrainEvents();
        session.ClearFallbackPlaylist();

        Assert.Null(session.FallbackPlaylistId);
        Assert.False(session.FallbackShuffle);
        Assert.Equal(new FallbackPlaylistCleared(session.Id, "first"), Assert.Single(session.DrainEvents()));
    }

    [Fact]
    public void Release_RemovesTheHostAndTheirPlaylist_NamingBoth()
    {
        var session = NewSession();
        session.HostProfileId = "host-1";
        session.SetFallbackPlaylist("first", "First", shuffle: true);
        session.DrainEvents();

        session.Release();

        Assert.Null(session.HostProfileId);
        Assert.Null(session.FallbackPlaylistId);
        Assert.Collection(
            session.DrainEvents(),
            first => Assert.Equal(new HostReleased(session.Id, "host-1"), first),
            second => Assert.Equal(new FallbackPlaylistCleared(session.Id, "first"), second));
    }

    [Fact]
    public void Release_WithoutAHost_StillRaisesHostReleased()
    {
        var session = NewSession();

        session.Release();

        Assert.Equal(new HostReleased(session.Id, null), Assert.Single(session.DrainEvents()));
    }

    [Fact]
    public void ClaimHost_OfASessionWithoutAHost_MakesTheProfileTheHost_AndRaisesHostClaimed()
    {
        var session = NewSession();

        var outcome = session.ClaimHost("host-1");

        Assert.Equal(HostClaimOutcome.Claimed, outcome);
        Assert.Equal("host-1", session.HostProfileId);
        Assert.Equal(new HostClaimed(session.Id, "host-1"), Assert.Single(session.DrainEvents()));
    }

    [Fact]
    public void ClaimHost_ByTheCurrentHost_ChangesAndRaisesNothing()
    {
        var session = NewSession();
        session.ClaimHost("host-1");
        session.DrainEvents();

        var outcome = session.ClaimHost("host-1");

        Assert.Equal(HostClaimOutcome.AlreadyHost, outcome);
        Assert.Equal("host-1", session.HostProfileId);
        Assert.Empty(session.DrainEvents());
    }

    [Fact]
    public void ClaimHost_ByAnotherProfile_IsRefused_AndKeepsTheFirstHost()
    {
        var session = NewSession();
        session.ClaimHost("host-1");
        session.DrainEvents();

        var outcome = session.ClaimHost("host-2");

        Assert.Equal(HostClaimOutcome.HostedByAnother, outcome);
        Assert.Equal("host-1", session.HostProfileId);
        Assert.Empty(session.DrainEvents());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ClaimHost_WithoutAProfileId_Throws(string? profileId)
    {
        Assert.ThrowsAny<ArgumentException>(() => NewSession().ClaimHost(profileId!));
    }

    [Fact]
    public void ClaimHost_AfterARelease_LetsAnotherProfileClaim()
    {
        var session = NewSession();
        session.ClaimHost("host-1");
        session.Release();

        Assert.Equal(HostClaimOutcome.Claimed, session.ClaimHost("host-2"));
    }

    private static Song Track(string id)
    {
        return new Song
        {
            Id = id,
            Uri = $"spotify:track:{id}",
            Name = id,
            Artists = [],
            Album = new Album { Name = "Album", Artists = [] }
        };
    }

    private static Session PlaylistSession(bool shuffle, int cursor = 0)
    {
        var session = NewSession();
        session.SetFallbackPlaylist("playlist", "Mix", shuffle);
        session.FallbackCursor = cursor;
        return session;
    }

    [Fact]
    public void NextFallbackTrack_InOrder_TakesTheTrackAtTheCursor_AndAdvancesIt()
    {
        var session = PlaylistSession(shuffle: false, cursor: 1);

        var pick = session.NextFallbackTrack([Track("a"), Track("b"), Track("c")], null, new Random(1));

        Assert.Equal("spotify:track:b", pick!.Track.Uri);
        Assert.Equal(2, pick.NextCursor);
    }

    [Fact]
    public void NextFallbackTrack_AfterTheLastTrack_WrapsToTheFirst()
    {
        var session = PlaylistSession(shuffle: false, cursor: 2);

        var pick = session.NextFallbackTrack([Track("a"), Track("b"), Track("c")], null, new Random(1));

        Assert.Equal("spotify:track:c", pick!.Track.Uri);
        Assert.Equal(0, pick.NextCursor);
    }

    [Fact]
    public void NextFallbackTrack_WithACursorBeyondAShorterPlaylist_WrapsIt()
    {
        var session = PlaylistSession(shuffle: false, cursor: 7);

        var pick = session.NextFallbackTrack([Track("a"), Track("b"), Track("c")], null, new Random(1));

        Assert.Equal("spotify:track:b", pick!.Track.Uri);
        Assert.Equal(2, pick.NextCursor);
    }

    [Fact]
    public void NextFallbackTrack_DoesNotMoveTheCursorItself()
    {
        var session = PlaylistSession(shuffle: false, cursor: 1);

        session.NextFallbackTrack([Track("a"), Track("b")], null, new Random(1));

        Assert.Equal(1, session.FallbackCursor);
    }

    [Fact]
    public void NextFallbackTrack_WhenShuffled_NeverPicksThePlayingTrack_AndHasNoCursor()
    {
        var session = PlaylistSession(shuffle: true);
        var tracks = new[] { Track("a"), Track("b"), Track("c") };
        var random = new Random(7);

        for (var i = 0; i < 50; i++)
        {
            var pick = session.NextFallbackTrack(tracks, "spotify:track:a", random);

            Assert.NotEqual("spotify:track:a", pick!.Track.Uri);
            Assert.Null(pick.NextCursor);
        }
    }

    [Fact]
    public void NextFallbackTrack_WhenShuffled_CanPickEveryOtherTrack()
    {
        var session = PlaylistSession(shuffle: true);
        var tracks = new[] { Track("a"), Track("b"), Track("c") };
        var random = new Random(7);

        var picked = Enumerable.Range(0, 100)
            .Select(_ => session.NextFallbackTrack(tracks, "spotify:track:a", random)!.Track.Uri)
            .ToHashSet();

        Assert.Equal(["spotify:track:b", "spotify:track:c"], picked.Order());
    }

    [Fact]
    public void NextFallbackTrack_WhenShuffledWithOnlyThePlayingTrack_PicksItAnyway()
    {
        var session = PlaylistSession(shuffle: true);

        var pick = session.NextFallbackTrack([Track("a")], "spotify:track:a", new Random(1));

        Assert.Equal("spotify:track:a", pick!.Track.Uri);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NextFallbackTrack_WithoutTracks_PicksNothing(bool shuffle)
    {
        var session = PlaylistSession(shuffle);

        Assert.Null(session.NextFallbackTrack([], null, new Random(1)));
    }

    private static readonly Guid SavedPlaylistId = Guid.NewGuid();

    private static IReadOnlyList<SavedPlaylistItem> SavedSongs(params string[] urls)
    {
        var queue = urls
            .Select((url, index) => new QueueItem(
                Guid.NewGuid(),
                index % 2 == 0 ? QueueItemSource.Youtube : QueueItemSource.Spotify,
                url,
                url,
                null,
                null,
                null)
            {
                Id = index + 1
            })
            .ToList();

        return SavedPlaylist.Create(Guid.NewGuid(), Guid.NewGuid(), "Mix", queue, 0, Now).Playlist!.Items;
    }

    private static Session SavedPlaylistSession(bool shuffle, int cursor = 0)
    {
        var session = NewSession();
        session.SetFallbackSavedPlaylist(SavedPlaylistId, "Mix", shuffle);
        session.FallbackCursor = cursor;
        return session;
    }

    [Fact]
    public void SetFallbackSavedPlaylist_RewindsItAndStoresItsIdWithTheSavedKind()
    {
        var session = NewSession();
        session.SetFallbackSavedPlaylist(SavedPlaylistId, "First", shuffle: false);
        session.FallbackCursor = 4;
        session.DrainEvents();

        session.SetFallbackSavedPlaylist(SavedPlaylistId, "Second", shuffle: true);

        Assert.Equal(
            (SavedPlaylistId.ToString(), FallbackPlaylistKind.Saved, SavedPlaylistId, "Second", true, 0),
            (session.FallbackPlaylistId, session.FallbackPlaylistKind, session.FallbackSavedPlaylistId, session.FallbackPlaylistName, session.FallbackShuffle, session.FallbackCursor));
        Assert.Equal(
            new FallbackPlaylistChanged(session.Id, FallbackPlaylistKind.Saved, SavedPlaylistId.ToString(), null),
            Assert.Single(session.DrainEvents()));
    }

    [Fact]
    public void SetFallbackSavedPlaylist_ReplacesASpotifyPlaylist_AndNamesIt()
    {
        var session = NewSession();
        session.SetFallbackPlaylist("spotify-1", "Spotify", shuffle: true);
        session.DrainEvents();

        session.SetFallbackSavedPlaylist(SavedPlaylistId, "Mine", shuffle: false);

        Assert.Equal((SavedPlaylistId.ToString(), FallbackPlaylistKind.Saved), (session.FallbackPlaylistId, session.FallbackPlaylistKind));
        Assert.Equal(SavedPlaylistId, session.FallbackSavedPlaylistId);
        Assert.False(session.FallbackShuffle);
        Assert.Equal(
            new FallbackPlaylistChanged(session.Id, FallbackPlaylistKind.Saved, SavedPlaylistId.ToString(), "spotify-1"),
            Assert.Single(session.DrainEvents()));
    }

    [Fact]
    public void SetFallbackPlaylist_ReplacesASavedPlaylist()
    {
        var session = SavedPlaylistSession(shuffle: true, cursor: 3);
        session.DrainEvents();

        session.SetFallbackPlaylist("spotify-1", "Spotify", shuffle: false);

        Assert.Null(session.FallbackSavedPlaylistId);
        Assert.Equal(FallbackPlaylistKind.Spotify, session.FallbackPlaylistKind);
        Assert.Equal(("spotify-1", "Spotify", false, 0), (session.FallbackPlaylistId, session.FallbackPlaylistName, session.FallbackShuffle, session.FallbackCursor));
        Assert.Equal(new FallbackPlaylistChanged(session.Id, FallbackPlaylistKind.Spotify, "spotify-1", null), Assert.Single(session.DrainEvents()));
    }

    [Fact]
    public void SetFallbackSavedPlaylist_WithoutAnIdOrAName_Throws()
    {
        var session = NewSession();

        Assert.Throws<ArgumentException>(() => session.SetFallbackSavedPlaylist(Guid.Empty, "Mine", shuffle: false));
        Assert.Throws<ArgumentException>(() => session.SetFallbackSavedPlaylist(SavedPlaylistId, " ", shuffle: false));
        Assert.Null(session.FallbackSavedPlaylistId);
        Assert.Empty(session.DrainEvents());
    }

    [Fact]
    public void ClearFallbackPlaylist_OfASavedPlaylist_RaisesWithoutASpotifyId()
    {
        var session = SavedPlaylistSession(shuffle: true, cursor: 2);
        session.DrainEvents();

        session.ClearFallbackPlaylist();

        Assert.Null(session.FallbackSavedPlaylistId);
        Assert.Null(session.FallbackPlaylistId);
        Assert.Null(session.FallbackPlaylistKind);
        Assert.Null(session.FallbackPlaylistName);
        Assert.False(session.FallbackShuffle);
        Assert.Equal(0, session.FallbackCursor);
        Assert.Equal(new FallbackPlaylistCleared(session.Id, null), Assert.Single(session.DrainEvents()));
    }

    [Fact]
    public void Release_ClearsASavedPlaylistToo()
    {
        var session = SavedPlaylistSession(shuffle: false);
        session.HostProfileId = "host-1";
        session.DrainEvents();

        session.Release();

        Assert.Null(session.FallbackSavedPlaylistId);
        Assert.Collection(
            session.DrainEvents(),
            first => Assert.Equal(new HostReleased(session.Id, "host-1"), first),
            second => Assert.Equal(new FallbackPlaylistCleared(session.Id, null), second));
    }

    [Fact]
    public void ASpotifyPlaylist_HasNoSavedPlaylistId_EvenWhenItsIdLooksLikeAGuid()
    {
        var session = NewSession();
        session.SetFallbackPlaylist(SavedPlaylistId.ToString(), "Spotify", shuffle: false);

        Assert.Equal(FallbackPlaylistKind.Spotify, session.FallbackPlaylistKind);
        Assert.Null(session.FallbackSavedPlaylistId);
    }

    [Fact]
    public void NextFallbackSavedSong_InOrder_TakesTheSongAtTheCursor_AndWrapsIt()
    {
        var songs = SavedSongs("a", "b", "c");

        var middle = SavedPlaylistSession(shuffle: false, cursor: 1).NextFallbackSavedSong(songs, null, new Random(1));
        var last = SavedPlaylistSession(shuffle: false, cursor: 2).NextFallbackSavedSong(songs, null, new Random(1));
        var beyond = SavedPlaylistSession(shuffle: false, cursor: 7).NextFallbackSavedSong(songs, null, new Random(1));

        Assert.Equal(("b", 2), (middle!.Song.Url, middle.NextCursor));
        Assert.Equal(("c", 0), (last!.Song.Url, last.NextCursor));
        Assert.Equal(("b", 2), (beyond!.Song.Url, beyond.NextCursor));
    }

    [Fact]
    public void NextFallbackSavedSong_KeepsTheSourceOfTheSong()
    {
        var songs = SavedSongs("a", "b");

        var first = SavedPlaylistSession(shuffle: false, cursor: 0).NextFallbackSavedSong(songs, null, new Random(1));
        var second = SavedPlaylistSession(shuffle: false, cursor: 1).NextFallbackSavedSong(songs, null, new Random(1));

        Assert.Equal(QueueItemSource.Youtube, first!.Song.Source);
        Assert.Equal(QueueItemSource.Spotify, second!.Song.Source);
    }

    [Fact]
    public void NextFallbackSavedSong_WhenShuffled_NeverPicksThePlayingSong_AndHasNoCursor()
    {
        var session = SavedPlaylistSession(shuffle: true);
        var songs = SavedSongs("a", "b", "c");
        var random = new Random(7);

        var picked = Enumerable.Range(0, 100)
            .Select(_ => session.NextFallbackSavedSong(songs, "a", random)!)
            .ToList();

        Assert.All(picked, x => Assert.Null(x.NextCursor));
        Assert.Equal(["b", "c"], picked.Select(x => x.Song.Url).ToHashSet().Order());
    }

    [Fact]
    public void NextFallbackSavedSong_WhenShuffledWithOnlyThePlayingSong_PicksItAnyway()
    {
        var session = SavedPlaylistSession(shuffle: true);

        var pick = session.NextFallbackSavedSong(SavedSongs("a"), "a", new Random(1));

        Assert.Equal("a", pick!.Song.Url);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NextFallbackSavedSong_WithoutSongs_PicksNothing(bool shuffle)
    {
        var session = SavedPlaylistSession(shuffle);

        Assert.Null(session.NextFallbackSavedSong([], null, new Random(1)));
    }

    [Fact]
    public void NextFallbackSavedSong_DoesNotMoveTheCursorItself()
    {
        var session = SavedPlaylistSession(shuffle: false, cursor: 1);

        session.NextFallbackSavedSong(SavedSongs("a", "b"), null, new Random(1));

        Assert.Equal(1, session.FallbackCursor);
    }

    [Fact]
    public void DrainEvents_ForgetsWhatItReturned()
    {
        var session = NewSession();
        session.Rename("Friday");

        Assert.Single(session.DrainEvents());
        Assert.Empty(session.DrainEvents());
    }
}
