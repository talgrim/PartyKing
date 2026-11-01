using Microsoft.Extensions.Logging.Abstractions;
using ErrorOr;
using PartyKing.Application.Queue.Services;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Tests;

public class QueueServiceNextTests
{
    private const string HostId = "host-profile";

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeQueueRepository _queue = new();
    private readonly FakeQueueNotifier _notifier = new();
    private readonly QueueService _service;
    private readonly Session _session;

    public QueueServiceNextTests()
    {
        // Advancing needs neither Spotify, YouTube nor host tokens
        _service = new QueueService(_queue, _sessions, null!, null!, null!, _notifier, new FakeFallbackQueueFiller(), NullLogger<QueueService>.Instance);
        _session = _sessions.AddSession(HostId);
    }

    [Fact]
    public async Task GetNext_PassesTheSongThePlayerThinksIsPlaying()
    {
        _queue.Playing = new QueueItem(_session.Id, QueueItemSource.Youtube, "url", "Old", null, null, null)
        {
            Id = 8,
            Status = QueueItemStatus.Playing
        };
        _queue.AdvanceResult = new QueueItem(_session.Id, QueueItemSource.Youtube, "url", "Title", null, null, null) { Id = 9 };

        var result = await _service.GetNextAsync(_session.Id, HostId, 8, CancellationToken.None);

        Assert.Equal(9, result.Value.Id);
        Assert.Equal((_session.Id, (int?)8), Assert.Single(_queue.AdvanceCalls));
        var advanced = Assert.Single(_notifier.QueueAdvanced);
        Assert.Equal(_session.Id, advanced.SessionId);
        Assert.Equal(8, advanced.PreviousSongId);
        Assert.Equal(9, advanced.NowPlaying?.Id);
    }

    [Fact]
    public async Task GetNext_ByAnotherAccount_IsForbiddenAndAdvancesNothing()
    {
        var result = await _service.GetNextAsync(_session.Id, "someone-else", null, CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Empty(_queue.AdvanceCalls);
        Assert.Empty(_notifier.QueueAdvanced);
    }

    [Fact]
    public async Task GetNext_EmptyQueue_IsNotFound()
    {
        _queue.Playing = new QueueItem(_session.Id, QueueItemSource.Youtube, "url", "Last", null, null, null)
        {
            Id = 1,
            Status = QueueItemStatus.Playing
        };
        _queue.AdvanceResult = null;

        var result = await _service.GetNextAsync(_session.Id, HostId, 1, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        var advanced = Assert.Single(_notifier.QueueAdvanced);
        Assert.Equal(1, advanced.PreviousSongId);
        Assert.Null(advanced.NowPlaying);
    }

    [Fact]
    public async Task GetNext_StaleCurrentSong_DoesNotNotify()
    {
        _queue.Playing = new QueueItem(_session.Id, QueueItemSource.Youtube, "url", "Already next", null, null, null)
        {
            Id = 9,
            Status = QueueItemStatus.Playing
        };
        _queue.AdvanceResult = _queue.Playing;

        var result = await _service.GetNextAsync(_session.Id, HostId, 8, CancellationToken.None);

        Assert.Equal(9, result.Value.Id);
        Assert.Empty(_notifier.QueueAdvanced);
    }
}
