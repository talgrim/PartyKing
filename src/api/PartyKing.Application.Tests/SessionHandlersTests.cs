using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Queue.Services;
using PartyKing.Application.Sessions.Handlers;
using PartyKing.Domain.Events.Sessions;

namespace PartyKing.Application.Tests;

public class SessionHandlersTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly Guid SessionId = Guid.NewGuid();

    private readonly FakeHostSessionCleanup _cleanup = new();
    private readonly FakeSlideshowService _slideshow = new();
    private readonly PlaylistTrackCache _cache = new(new FakeClock());
    private readonly FallbackStatusStore _status = new(new FakePlayerNotifier(), NullLogger<FallbackStatusStore>.Instance);
    private readonly FakeFallbackQueueFiller _filler = new();

    private FallbackPlaylistHandler FallbackHandler() => new(_cache, _status, _filler);

    [Fact]
    public async Task HostReleased_ForgetsTheHostThatWent()
    {
        await new HostCleanupHandler(_cleanup).HandleAsync(new HostReleased(SessionId, "host-1"), Ct);

        Assert.Equal((SessionId, "host-1"), Assert.Single(_cleanup.Forgotten));
    }

    [Fact]
    public async Task SessionDeleted_ForgetsTheHostAndRemovesTheSlideshow()
    {
        var deleted = new SessionDeleted(SessionId, "host-1", null);

        await new HostCleanupHandler(_cleanup).HandleAsync(deleted, Ct);
        await new SlideshowCleanupHandler(_slideshow).HandleAsync(deleted, Ct);

        Assert.Equal((SessionId, "host-1"), Assert.Single(_cleanup.Forgotten));
        Assert.Equal([SessionId], _slideshow.DeletedSessions);
    }

    [Fact]
    public async Task ANewPlaylist_DropsTheCachedTracksOfBothPlaylists_TheOldErrorAndFillsTheQueue()
    {
        _cache.Set("old", []);
        _cache.Set("new", []);
        _status.SetError(SessionId, "old error");

        await FallbackHandler().HandleAsync(new FallbackPlaylistChanged(SessionId, "old", "new"), Ct);

        Assert.False(_cache.TryGet("old", out _));
        Assert.False(_cache.TryGet("new", out _));
        Assert.Null(_status.GetError(SessionId));
        Assert.Equal(SessionId, Assert.Single(_filler.Ensured));
    }

    [Fact]
    public async Task ClearingThePlaylist_DropsItsTracksAndTheError_WithoutFillingTheQueue()
    {
        _cache.Set("old", []);
        _status.SetError(SessionId, "old error");

        await FallbackHandler().HandleAsync(new FallbackPlaylistCleared(SessionId, "old"), Ct);

        Assert.False(_cache.TryGet("old", out _));
        Assert.Null(_status.GetError(SessionId));
        Assert.Empty(_filler.Ensured);
    }

    [Fact]
    public async Task ADeletedSession_LeavesNoFallbackStateBehind()
    {
        _cache.Set("old", []);
        _status.SetError(SessionId, "old error");

        await FallbackHandler().HandleAsync(new SessionDeleted(SessionId, null, "old"), Ct);

        Assert.False(_cache.TryGet("old", out _));
        Assert.Null(_status.GetError(SessionId));
    }

    [Fact]
    public async Task ADeletedSessionWithoutAPlaylist_IsFine()
    {
        await FallbackHandler().HandleAsync(new SessionDeleted(SessionId, null, null), Ct);

        Assert.Null(_status.GetError(SessionId));
    }
}
