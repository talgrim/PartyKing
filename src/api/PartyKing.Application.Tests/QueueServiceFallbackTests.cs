using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Queue.Services;
using PartyKing.Application.Sessions;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Tests;

public class QueueServiceFallbackTests
{
    private const string HostId = "host-profile";

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeQueueRepository _queue = new();
    private readonly FakeFallbackQueueFiller _filler = new();
    private readonly QueueService _service;
    private readonly Guid _id;

    public QueueServiceFallbackTests()
    {
        _service = new QueueService(_queue, _sessions, null!, null!, null!, new FakeSessionMusicNotifier(), _filler, NullLogger<QueueService>.Instance);
        _id = _sessions.AddSession(HostId).Id;
    }

    [Fact]
    public async Task GetNext_TopsUpTheQueueBeforeAndAfterAdvancing()
    {
        _queue.Playing = new QueueItem(_id, QueueItemSource.Spotify, "uri", "Old", null, null, null) { Id = 1, Status = QueueItemStatus.Playing };
        _queue.AdvanceResult = new QueueItem(_id, QueueItemSource.Spotify, "uri", "New", null, null, null) { Id = 2 };

        await _service.GetNextAsync(_id, HostId, 1, CancellationToken.None);

        Assert.Equal(2, _filler.Ensured.Count);
    }

    [Fact]
    public async Task Remove_TopsUpTheQueue()
    {
        await _service.RemoveAsync(_id, 5, new SessionCaller(HostId, false), CancellationToken.None);

        Assert.Equal(_id, Assert.Single(_filler.Ensured));
    }

    [Fact]
    public async Task Remove_OfASongThatIsNoLongerWaiting_DoesNotTopUp()
    {
        _queue.RemoveResult = QueueItemRemoval.NotQueued;

        await _service.RemoveAsync(_id, 5, new SessionCaller(HostId, false), CancellationToken.None);

        Assert.Empty(_filler.Ensured);
    }
}
