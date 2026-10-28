using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Queue.Services;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Enums;
using PartyKing.Infrastructure.Spotify.RestApi;

namespace PartyKing.Application.Tests;

public class FallbackQueueFillerTests
{
    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeQueueRepository _queue = new();
    private readonly FakeHostTokenProvider _tokens = new();
    private readonly FakePlaylistSpotifyClient _spotify = new();
    private readonly FallbackStatusStore _status = new(new FakeSessionMusicNotifier(), NullLogger<FallbackStatusStore>.Instance);
    private readonly FakeSessionMusicNotifier _notifier = new();
    private readonly FallbackQueueFiller _filler;
    private readonly Session _session;

    public FallbackQueueFillerTests()
    {
        _filler = new FallbackQueueFiller(
            _sessions,
            _queue,
            _spotify,
            _tokens,
            new PlaylistTrackCache(new FakeClock()),
            _status,
            _notifier);
        _session = _sessions.AddSession("host-1");
        _spotify.Tracks = [FakePlaylistSpotifyClient.Track("a"), FakePlaylistSpotifyClient.Track("b"), FakePlaylistSpotifyClient.Track("c")];
    }

    [Fact]
    public async Task Ensure_WithoutAFallbackPlaylist_AddsNothing()
    {
        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Empty(_queue.Added);
        Assert.Equal(0, _spotify.TrackCalls);
    }

    [Fact]
    public async Task Ensure_WhileASongWaits_AddsNothing()
    {
        _session.FallbackPlaylistId = "playlist";
        _queue.Pending.Add(new QueueItem(_session.Id, QueueItemSource.Youtube, "url", "Guest song", null, null, null));

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Empty(_queue.Added);
        Assert.Equal(0, _spotify.TrackCalls);
    }

    [Fact]
    public async Task Ensure_WithAnEmptyQueue_QueuesTheNextPlaylistTrackAsFallback()
    {
        _session.FallbackPlaylistId = "playlist";
        _session.FallbackCursor = 1;

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        var added = Assert.Single(_queue.Added);
        Assert.Equal("spotify:track:b", added.Url);
        Assert.True(added.IsFallback);
        Assert.Equal(QueueItemSource.Spotify, added.Source);
        Assert.Equal(2, _session.FallbackCursor);
        Assert.True(Assert.Single(_notifier.SongAdded).Song.IsFallback);
    }

    [Fact]
    public async Task Ensure_AfterTheLastTrack_StartsOverFromTheFirst()
    {
        _session.FallbackPlaylistId = "playlist";
        _session.FallbackCursor = 2;

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Equal("spotify:track:c", Assert.Single(_queue.Added).Url);
        Assert.Equal(0, _session.FallbackCursor);
    }

    [Fact]
    public async Task Ensure_WhenShuffled_QueuesATrackButNotTheOneThatPlays()
    {
        _session.FallbackPlaylistId = "playlist";
        _session.FallbackShuffle = true;
        _queue.Playing = new QueueItem(_session.Id, QueueItemSource.Spotify, "spotify:track:a", "A", null, null, null)
        {
            Status = QueueItemStatus.Playing
        };

        for (var i = 0; i < 20; i++)
        {
            _queue.Pending.Clear();
            await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);
        }

        Assert.Equal(20, _queue.Added.Count);
        Assert.DoesNotContain(_queue.Added, x => x.Url == "spotify:track:a");
        Assert.Empty(_sessions.FallbackCursorSet);
    }

    [Fact]
    public async Task Ensure_ReadsThePlaylistOnlyOnceWhileItIsCached()
    {
        _session.FallbackPlaylistId = "playlist";

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);
        _queue.Pending.Clear();
        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Equal(2, _queue.Added.Count);
        Assert.Equal(1, _spotify.TrackCalls);
    }

    [Fact]
    public async Task Ensure_WhenTheHostTokenIsUnavailable_RecordsTheErrorAndAddsNothing()
    {
        _session.FallbackPlaylistId = "playlist";
        _tokens.SessionToken = ErrorOr.Error.Conflict(description: "The session has no host");

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Empty(_queue.Added);
        Assert.Contains("no host", _status.GetError(_session.Id));
    }

    [Fact]
    public async Task Ensure_WithAnEmptyPlaylist_RecordsTheError()
    {
        _session.FallbackPlaylistId = "playlist";
        _spotify.Tracks = [];

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Empty(_queue.Added);
        Assert.Contains("no playable tracks", _status.GetError(_session.Id));
    }

    [Fact]
    public async Task Ensure_WhenTheSongIsQueuedAfterwards_ClearsAnEarlierError()
    {
        _session.FallbackPlaylistId = "playlist";
        _status.SetError(_session.Id, "old error");

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Null(_status.GetError(_session.Id));
    }

    [Fact]
    public async Task Ensure_WhenAGuestQueuedASongMeanwhile_KeepsTheCursor()
    {
        _session.FallbackPlaylistId = "playlist";
        _spotify.OnReadTracks = () => _queue.Pending.Add(
            new QueueItem(_session.Id, QueueItemSource.Youtube, "url", "Guest song", null, null, null));

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Empty(_queue.Added);
        Assert.Equal(0, _session.FallbackCursor);
        Assert.Empty(_notifier.SongAdded);
    }
}

internal class FakePlaylistSpotifyClient : ISpotifyIntegrationClient
{
    public IReadOnlyList<Song> Tracks { get; set; } = [];
    public Playlist? Playlist { get; set; }
    public IReadOnlyList<Playlist> UserPlaylists { get; set; } = [];
    public int TrackCalls { get; private set; }
    public Action? OnReadTracks { get; set; }

    public static Song Track(string id) => new()
    {
        Id = id,
        Uri = $"spotify:track:{id}",
        Name = id.ToUpperInvariant(),
        Artists = [new Artist { Name = "Artist" }],
        Album = new Album { Name = "Album", Artists = [] }
    };

    public Task<Playlist?> GetPlaylistAsync(string accessToken, string playlistId, CancellationToken cancellationToken) =>
        Task.FromResult(Playlist);

    public Task<IReadOnlyList<Playlist>> GetCurrentUserPlaylistsAsync(string accessToken, CancellationToken cancellationToken) =>
        Task.FromResult(UserPlaylists);

    public Task<IReadOnlyList<Song>> GetPlaylistTracksAsync(string accessToken, string playlistId, CancellationToken cancellationToken)
    {
        TrackCalls++;
        OnReadTracks?.Invoke();
        return Task.FromResult(Tracks);
    }

    public Uri GetAuthenticationUri(string state) => throw new NotSupportedException();
    public Task<PartyKing.Domain.Results.AuthenticationResult> AuthenticateAsync(string code, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<PartyKing.Domain.Results.ReAuthenticationResult> ReAuthenticateAsync(string refreshToken, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<Song>> SearchTracksAsync(string accessToken, string query, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<Song?> GetTrackAsync(string accessToken, string trackId, CancellationToken cancellationToken) => throw new NotSupportedException();
}
