using PartyKing.Application.Ports;
using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Queue.Services;
using PartyKing.Domain.Entities.Playlists;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Tests;

public class FallbackQueueFillerTests
{
    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeSavedPlaylistsRepository _savedPlaylists = new();
    private readonly FakeQueueRepository _queue = new();
    private readonly FakeHostTokenProvider _tokens = new();
    private readonly FakePlaylistSpotifyClient _spotify = new();
    private readonly FallbackStatusStore _status = new(new FakePlayerNotifier(), NullLogger<FallbackStatusStore>.Instance);
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
            _savedPlaylists);
        _session = _sessions.AddSession("host-1");
        _spotify.Tracks = [FakePlaylistSpotifyClient.Track("a"), FakePlaylistSpotifyClient.Track("b"), FakePlaylistSpotifyClient.Track("c")];
    }

    private SavedPlaylist SaveAPlaylist(params (QueueItemSource Source, string Url)[] songs)
    {
        var queue = songs
            .Select((song, index) => new QueueItem(
                Guid.NewGuid(),
                song.Source,
                song.Url,
                $"Song {index}",
                "Artist",
                "https://art",
                1000)
            {
                Id = index + 1
            })
            .ToList();
        var playlist = SavedPlaylist.Create(Guid.NewGuid(), Guid.NewGuid(), "Mine", queue, 0, DateTime.UtcNow).Playlist!;
        _savedPlaylists.Saved.Add(playlist);

        return playlist;
    }

    private SavedPlaylist ChooseASavedPlaylist(bool shuffle = false, int cursor = 0)
    {
        var playlist = SaveAPlaylist(
            (QueueItemSource.Youtube, "https://youtu.be/a"),
            (QueueItemSource.Spotify, "spotify:track:b"),
            (QueueItemSource.Youtube, "https://youtu.be/c"));
        _session.SetFallbackSavedPlaylist(playlist.Id, playlist.Name, shuffle);
        _session.FallbackCursor = cursor;

        return playlist;
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
        _session.SetFallbackPlaylist("playlist", "Mix", shuffle: false);
        _queue.Pending.Add(new QueueItem(_session.Id, QueueItemSource.Youtube, "url", "Guest song", null, null, null));

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Empty(_queue.Added);
        Assert.Equal(0, _spotify.TrackCalls);
    }

    [Fact]
    public async Task Ensure_WithAnEmptyQueue_QueuesTheNextPlaylistTrackAsFallback()
    {
        _session.SetFallbackPlaylist("playlist", "Mix", shuffle: false);
        _session.FallbackCursor = 1;

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        var added = Assert.Single(_queue.Added);
        Assert.Equal("spotify:track:b", added.Url);
        Assert.True(added.IsFallback);
        Assert.Equal(QueueItemSource.Spotify, added.Source);
        Assert.Equal(2, _session.FallbackCursor);
    }

    [Fact]
    public async Task Ensure_AfterTheLastTrack_StartsOverFromTheFirst()
    {
        _session.SetFallbackPlaylist("playlist", "Mix", shuffle: false);
        _session.FallbackCursor = 2;

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Equal("spotify:track:c", Assert.Single(_queue.Added).Url);
        Assert.Equal(0, _session.FallbackCursor);
    }

    [Fact]
    public async Task Ensure_WhenShuffled_QueuesATrackButNotTheOneThatPlays()
    {
        _session.SetFallbackPlaylist("playlist", "Mix", shuffle: false);
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
        _session.SetFallbackPlaylist("playlist", "Mix", shuffle: false);

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);
        _queue.Pending.Clear();
        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Equal(2, _queue.Added.Count);
        Assert.Equal(1, _spotify.TrackCalls);
    }

    [Fact]
    public async Task Ensure_WhenTheHostTokenIsUnavailable_RecordsTheErrorAndAddsNothing()
    {
        _session.SetFallbackPlaylist("playlist", "Mix", shuffle: false);
        _tokens.SessionToken = ErrorOr.Error.Conflict(description: "The session has no host");

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Empty(_queue.Added);
        Assert.Contains("no host", _status.GetError(_session.Id));
    }

    [Fact]
    public async Task Ensure_WithAnEmptyPlaylist_RecordsTheError()
    {
        _session.SetFallbackPlaylist("playlist", "Mix", shuffle: false);
        _spotify.Tracks = [];

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Empty(_queue.Added);
        Assert.Contains("no playable tracks", _status.GetError(_session.Id));
    }

    [Fact]
    public async Task Ensure_WhenTheSongIsQueuedAfterwards_ClearsAnEarlierError()
    {
        _session.SetFallbackPlaylist("playlist", "Mix", shuffle: false);
        _status.SetError(_session.Id, "old error");

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Null(_status.GetError(_session.Id));
    }

    [Fact]
    public async Task Ensure_WhenThePlaylistWasRewoundWhileSpotifyWasRead_KeepsTheRewind()
    {
        _session.SetFallbackPlaylist("playlist", "Mix", shuffle: false);
        _session.FallbackCursor = 2;
        _spotify.OnReadTracks = () => _session.SetFallbackPlaylist("playlist", "Mix", false);

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Single(_queue.Added);
        Assert.Equal(0, _session.FallbackCursor);
        Assert.Empty(_sessions.FallbackCursorSet);
    }

    [Fact]
    public async Task Ensure_WithASavedPlaylist_QueuesTheNextSongAsFallback_KeepingItsSource()
    {
        ChooseASavedPlaylist(cursor: 1);

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        var added = Assert.Single(_queue.Added);
        Assert.Equal((QueueItemSource.Spotify, "spotify:track:b", "Song 1", "Artist", "https://art", 1000), (added.Source, added.Url, added.Title, added.Artists, added.AlbumArtUrl, added.DurationMs));
        Assert.True(added.IsFallback);
        Assert.Equal(2, _session.FallbackCursor);
    }

    [Fact]
    public async Task Ensure_WithASavedPlaylist_QueuesAYoutubeSong()
    {
        ChooseASavedPlaylist(cursor: 0);

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        var added = Assert.Single(_queue.Added);
        Assert.Equal((QueueItemSource.Youtube, "https://youtu.be/a"), (added.Source, added.Url));
    }

    [Fact]
    public async Task Ensure_WithASavedPlaylist_NeedsNeitherSpotifyNorTheHostLogin()
    {
        ChooseASavedPlaylist();
        _tokens.SessionToken = ErrorOr.Error.Conflict(description: "The session has no host");

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Single(_queue.Added);
        Assert.Equal(0, _spotify.TrackCalls);
        Assert.Null(_status.GetError(_session.Id));
    }

    [Fact]
    public async Task Ensure_AfterTheLastSavedSong_StartsOverFromTheFirst()
    {
        ChooseASavedPlaylist(cursor: 2);

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Equal("https://youtu.be/c", Assert.Single(_queue.Added).Url);
        Assert.Equal(0, _session.FallbackCursor);
    }

    [Fact]
    public async Task Ensure_WithAShuffledSavedPlaylist_QueuesASongButNotTheOneThatPlays()
    {
        ChooseASavedPlaylist(shuffle: true);
        _queue.Playing = new QueueItem(_session.Id, QueueItemSource.Youtube, "https://youtu.be/a", "A", null, null, null)
        {
            Status = QueueItemStatus.Playing
        };

        for (var i = 0; i < 20; i++)
        {
            _queue.Pending.Clear();
            await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);
        }

        Assert.Equal(20, _queue.Added.Count);
        Assert.DoesNotContain(_queue.Added, x => x.Url == "https://youtu.be/a");
        Assert.Empty(_sessions.FallbackCursorSet);
    }

    [Fact]
    public async Task Ensure_WhileASongWaits_DoesNotReadTheSavedPlaylist()
    {
        ChooseASavedPlaylist();
        _queue.Pending.Add(new QueueItem(_session.Id, QueueItemSource.Youtube, "url", "Guest song", null, null, null));

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Empty(_queue.Added);
    }

    [Fact]
    public async Task Ensure_WhenTheSavedPlaylistIsGone_RecordsTheErrorAndAddsNothing()
    {
        ChooseASavedPlaylist();
        _savedPlaylists.Saved.Clear();

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Empty(_queue.Added);
        Assert.Contains("no longer exists", _status.GetError(_session.Id));
        Assert.NotNull(_session.FallbackSavedPlaylistId);
    }

    [Fact]
    public async Task Ensure_WhenASavedSongIsQueued_ClearsAnEarlierError()
    {
        ChooseASavedPlaylist();
        _status.SetError(_session.Id, "old error");

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Null(_status.GetError(_session.Id));
    }

    [Fact]
    public async Task Ensure_WhenAnotherPlaylistWasChosenWhileTheSavedOneWasRead_KeepsTheNewCursor()
    {
        ChooseASavedPlaylist(cursor: 2);
        _savedPlaylists.OnGet = () => _session.SetFallbackSavedPlaylist(Guid.NewGuid(), "Other", false);

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Single(_queue.Added);
        Assert.Equal(0, _session.FallbackCursor);
        Assert.Empty(_sessions.FallbackCursorSet);
    }

    [Fact]
    public async Task Ensure_WithASavedPlaylist_ThenASpotifyOne_ReadsSpotify()
    {
        ChooseASavedPlaylist();
        _session.SetFallbackPlaylist("playlist", "Mix", false);

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Equal("spotify:track:a", Assert.Single(_queue.Added).Url);
        Assert.Equal(1, _spotify.TrackCalls);
    }

    [Fact]
    public async Task Ensure_WhenAGuestQueuedASongMeanwhile_KeepsTheCursor()
    {
        _session.SetFallbackPlaylist("playlist", "Mix", shuffle: false);
        _spotify.OnReadTracks = () => _queue.Pending.Add(
            new QueueItem(_session.Id, QueueItemSource.Youtube, "url", "Guest song", null, null, null));

        await _filler.EnsureQueuedAsync(_session.Id, CancellationToken.None);

        Assert.Empty(_queue.Added);
        Assert.Equal(0, _session.FallbackCursor);
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
