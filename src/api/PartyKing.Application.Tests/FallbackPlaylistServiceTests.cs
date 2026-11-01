using Microsoft.Extensions.Logging.Abstractions;
using ErrorOr;
using PartyKing.Application.Queue.Services;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Sessions;
using PartyKing.Domain.Entities.Spotify;

namespace PartyKing.Application.Tests;

public class FallbackPlaylistServiceTests
{
    private const string HostId = "host-1";
    private const string PlaylistLink = "https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M";

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakePlaylistSpotifyClient _spotify = new();
    private readonly FakeHostTokenProvider _tokens = new();
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
            _filler,
            new PlaylistTrackCache(clock),
            _status);
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
}
