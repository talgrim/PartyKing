using Microsoft.Extensions.Logging.Abstractions;
using ErrorOr;
using PartyKing.Application.Queue.Services;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Tests;

public class QueueServiceRemoveTests
{
    private const string HostId = "host-profile";

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeQueueRepository _queue = new();
    private readonly FakeSessionMusicNotifier _notifier = new();
    private readonly QueueService _service;
    private readonly Session _session;

    public QueueServiceRemoveTests()
    {
        // Removing needs neither Spotify, YouTube nor host tokens
        _service = new QueueService(_queue, _sessions, null!, null!, null!, _notifier, new FakeFallbackQueueFiller(), NullLogger<QueueService>.Instance);
        _session = _sessions.AddSession(HostId);
    }

    [Fact]
    public async Task Remove_ByTheHost_RemovesTheSong()
    {
        var result = await _service.RemoveAsync(_session.Id, 4, HostId, false, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal((_session.Id, 4), Assert.Single(_queue.RemoveCalls));
        Assert.Equal((_session.Id, 4), Assert.Single(_notifier.SongRemoved));
    }

    [Fact]
    public async Task Remove_ByAnAdminWhoIsNotTheHost_RemovesTheSong()
    {
        var result = await _service.RemoveAsync(_session.Id, 4, null, true, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Single(_queue.RemoveCalls);
    }

    [Fact]
    public async Task Remove_ByAnotherAccount_IsForbiddenAndRemovesNothing()
    {
        var result = await _service.RemoveAsync(_session.Id, 4, "someone-else", false, CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Empty(_queue.RemoveCalls);
        Assert.Empty(_notifier.SongRemoved);
    }

    [Fact]
    public async Task Remove_ByAGuest_IsForbidden()
    {
        var result = await _service.RemoveAsync(_session.Id, 4, null, false, CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Empty(_queue.RemoveCalls);
    }

    [Fact]
    public async Task Remove_FromAMissingSession_IsNotFound()
    {
        var result = await _service.RemoveAsync(Guid.NewGuid(), 4, HostId, true, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Empty(_queue.RemoveCalls);
    }

    [Fact]
    public async Task Remove_OfAMissingSong_IsNotFound()
    {
        _queue.RemoveResult = QueueItemRemoval.NotFound;

        var result = await _service.RemoveAsync(_session.Id, 4, HostId, false, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task Remove_OfASongThatIsNoLongerWaiting_IsAConflict()
    {
        _queue.RemoveResult = QueueItemRemoval.NotQueued;

        var result = await _service.RemoveAsync(_session.Id, 4, HostId, false, CancellationToken.None);

        Assert.Equal(ErrorType.Conflict, result.FirstError.Type);
    }
}
