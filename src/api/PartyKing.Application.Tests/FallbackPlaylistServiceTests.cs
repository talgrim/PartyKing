using Microsoft.Extensions.Logging.Abstractions;
using ErrorOr;
using PartyKing.Application.Queue.Services;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Sessions;
using PartyKing.Domain.Entities.Playlists;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Tests;

public class FallbackPlaylistServiceTests
{
    private const string HostId = "host-1";
    private const string PlaylistLink = "https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M";

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakePlaylistSpotifyClient _spotify = new();
    private readonly FakeHostTokenProvider _tokens = new();
    private readonly FakeSavedPlaylistsRepository _savedPlaylists = new();
    private readonly FakeFallbackQueueFiller _filler = new();
    private readonly FallbackStatusStore _status = new(new FakePlayerNotifier(), NullLogger<FallbackStatusStore>.Instance);
    private readonly FallbackPlaylistService _service;
    private readonly Guid _id;

    public FallbackPlaylistServiceTests()
    {
        var clock = new FakeClock();
        var sessionAuthorizer = new SessionAuthorizer(_sessions);
        _service = new FallbackPlaylistService(
            sessionAuthorizer,
            _sessions,
            _spotify,
            _tokens,
            _savedPlaylists);
        _sessions.Events = TestEvents.SessionDispatcher(
            filler: _filler,
            trackCache: new PlaylistTrackCache(clock),
            statusStore: _status);
        _id = _sessions.AddSession(HostId).Id;
        _spotify.Playlist = new Playlist { Id = "37i9dQZF1DXcBWIGoYBM5M", Name = "Party mix", OwnerId = HostId, TrackCount = 12 };
    }

    [Fact]
    public async Task Set_AsTheHost_StoresThePlaylistAndFillsTheQueue()
    {
        var result = await _service.SetAsync(_id, PlaylistLink, true, new SessionCaller(HostId, false), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal((_id, "37i9dQZF1DXcBWIGoYBM5M", "Party mix", true), Assert.Single(_sessions.FallbackPlaylistSet));
        Assert.Equal(_id, Assert.Single(_filler.Ensured));
    }

    [Fact]
    public async Task Set_AsAnAdmin_Works()
    {
        var result = await _service.SetAsync(_id, PlaylistLink, false, new SessionCaller(null, true), CancellationToken.None);

        Assert.False(result.IsError);
    }

    [Fact]
    public async Task Set_AsAnyoneElse_IsForbidden()
    {
        var result = await _service.SetAsync(_id, PlaylistLink, false, new SessionCaller("someone", false), CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Empty(_sessions.FallbackPlaylistSet);
    }

    [Fact]
    public async Task Set_WithAPlaylistTheHostDoesNotOwn_IsRejected()
    {
        _spotify.Playlist = new Playlist { Id = "37i9dQZF1DXcBWIGoYBM5M", Name = "Not mine", OwnerId = "spotify", TrackCount = 50 };

        var result = await _service.SetAsync(_id, PlaylistLink, false, new SessionCaller(HostId, false), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Empty(_sessions.FallbackPlaylistSet);
    }

    [Fact]
    public async Task Set_AsAnAdmin_ChecksOwnershipAgainstTheSessionsHost()
    {
        _spotify.Playlist = new Playlist { Id = "37i9dQZF1DXcBWIGoYBM5M", Name = "Admin's", OwnerId = "admin-spotify", TrackCount = 5 };

        var result = await _service.SetAsync(_id, PlaylistLink, false, new SessionCaller(null, true), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task Set_WithAnUnknownPlaylist_IsNotFound()
    {
        _spotify.Playlist = null;

        var result = await _service.SetAsync(_id, PlaylistLink, false, new SessionCaller(HostId, false), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task Set_WithAnEmptyPlaylist_IsRejected()
    {
        _spotify.Playlist = new Playlist { Id = "37i9dQZF1DXcBWIGoYBM5M", Name = "Empty", OwnerId = HostId, TrackCount = 0 };

        var result = await _service.SetAsync(_id, PlaylistLink, false, new SessionCaller(HostId, false), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task Set_WithSomethingThatIsNotAPlaylistLink_IsRejected()
    {
        var result = await _service.SetAsync(_id, "https://example.com", false, new SessionCaller(HostId, false), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task Set_WhenTheSessionHasNoHost_PassesTheTokenError()
    {
        _tokens.SessionToken = Error.Conflict(description: "The session has no host");

        var result = await _service.SetAsync(_id, PlaylistLink, false, new SessionCaller(null, true), CancellationToken.None);

        Assert.Equal(ErrorType.Conflict, result.FirstError.Type);
    }

    [Fact]
    public async Task Set_WithoutAPlaylist_ClearsTheSetting()
    {
        await _service.SetAsync(_id, PlaylistLink, true, new SessionCaller(HostId, false), CancellationToken.None);
        _status.SetError(_id, "old error");

        var result = await _service.SetAsync(_id, "  ", false, new SessionCaller(HostId, false), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal((_id, (string?)null, (string?)null, false), _sessions.FallbackPlaylistSet.Last());
        Assert.Null(_status.GetError(_id));
    }

    [Fact]
    public async Task GetHostPlaylists_ListsOnlyThePlaylistsTheHostOwns()
    {
        _spotify.UserPlaylists =
        [
            new Playlist { Id = "mine", Name = "Mine", OwnerId = HostId, TrackCount = 3 },
            new Playlist { Id = "followed", Name = "Followed", OwnerId = "someone", TrackCount = 9 }
        ];

        var result = await _service.GetHostPlaylistsAsync(_id, HostId, CancellationToken.None);

        var playlist = Assert.Single(result.Value);
        Assert.Equal("spotify:playlist:mine", playlist.Uri);
    }

    [Fact]
    public async Task GetHostPlaylists_ForAnyoneButTheHost_IsForbidden()
    {
        var result = await _service.GetHostPlaylistsAsync(_id, "someone", CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
    }

    private static readonly Guid AccountId = Guid.NewGuid();

    private SavedPlaylist SaveAPlaylist(Guid ownerAccountId, string name = "Friday")
    {
        var songs = new List<QueueItem>
        {
            new(Guid.NewGuid(), QueueItemSource.Youtube, "https://youtu.be/a", "Song", null, null, null) { Id = 1 }
        };
        var playlist = SavedPlaylist.Create(Guid.NewGuid(), ownerAccountId, name, songs, 0, DateTime.UtcNow).Playlist!;
        _savedPlaylists.Saved.Add(playlist);

        return playlist;
    }

    [Fact]
    public async Task SetSaved_AsTheHost_StoresThePlaylistAndFillsTheQueue()
    {
        var playlist = SaveAPlaylist(AccountId);

        var result = await _service.SetSavedAsync(_id, playlist.Id, true, AccountId, new SessionCaller(HostId, false), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal((_id, playlist.Id, "Friday", true), Assert.Single(_sessions.FallbackSavedPlaylistSet));
        Assert.Equal(_id, Assert.Single(_filler.Ensured));
    }

    [Fact]
    public async Task SetSaved_AsAnAdmin_Works()
    {
        var playlist = SaveAPlaylist(AccountId);

        var result = await _service.SetSavedAsync(_id, playlist.Id, false, AccountId, new SessionCaller(null, true), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Single(_sessions.FallbackSavedPlaylistSet);
    }

    [Fact]
    public async Task SetSaved_AsAnyoneElse_IsForbidden()
    {
        var playlist = SaveAPlaylist(AccountId);

        var result = await _service.SetSavedAsync(_id, playlist.Id, false, AccountId, new SessionCaller("someone", false), CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Empty(_sessions.FallbackSavedPlaylistSet);
    }

    [Fact]
    public async Task SetSaved_WithAPlaylistOfAnotherAccount_IsNotFound()
    {
        var playlist = SaveAPlaylist(Guid.NewGuid());

        var result = await _service.SetSavedAsync(_id, playlist.Id, false, AccountId, new SessionCaller(HostId, false), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Empty(_sessions.FallbackSavedPlaylistSet);
    }

    [Fact]
    public async Task SetSaved_WithAnUnknownPlaylist_IsNotFound()
    {
        var result = await _service.SetSavedAsync(_id, Guid.NewGuid(), false, AccountId, new SessionCaller(HostId, false), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Empty(_sessions.FallbackSavedPlaylistSet);
    }

    [Fact]
    public async Task SetSaved_ForAnUnknownSession_IsNotFound()
    {
        var playlist = SaveAPlaylist(AccountId);

        var result = await _service.SetSavedAsync(Guid.NewGuid(), playlist.Id, false, AccountId, new SessionCaller(null, true), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Empty(_sessions.FallbackSavedPlaylistSet);
    }

    [Fact]
    public async Task SetSaved_ReplacesASpotifyPlaylist_AndDropsItsCachedTracks()
    {
        var clock = new FakeClock();
        var cache = new PlaylistTrackCache(clock);
        _sessions.Events = TestEvents.SessionDispatcher(filler: _filler, trackCache: cache, statusStore: _status);
        await _service.SetAsync(_id, PlaylistLink, false, new SessionCaller(HostId, false), CancellationToken.None);
        // Cached after the Spotify playlist was set, which dropped what was cached before
        cache.Set("37i9dQZF1DXcBWIGoYBM5M", []);
        var playlist = SaveAPlaylist(AccountId);

        await _service.SetSavedAsync(_id, playlist.Id, false, AccountId, new SessionCaller(HostId, false), CancellationToken.None);

        Assert.False(cache.TryGet("37i9dQZF1DXcBWIGoYBM5M", out _));
        var session = (await _sessions.GetAsync(_id, CancellationToken.None))!;
        Assert.Equal((FallbackPlaylistKind.Saved, playlist.Id.ToString()), (session.FallbackPlaylistKind, session.FallbackPlaylistId));
        Assert.Equal(playlist.Id, session.FallbackSavedPlaylistId);
    }

    [Fact]
    public async Task Set_WithoutAPlaylist_ClearsASavedPlaylistToo()
    {
        var playlist = SaveAPlaylist(AccountId);
        await _service.SetSavedAsync(_id, playlist.Id, false, AccountId, new SessionCaller(HostId, false), CancellationToken.None);

        var result = await _service.SetAsync(_id, null, false, new SessionCaller(HostId, false), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Null((await _sessions.GetAsync(_id, CancellationToken.None))!.FallbackSavedPlaylistId);
    }
}
