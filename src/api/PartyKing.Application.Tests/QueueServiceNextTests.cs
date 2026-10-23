using ErrorOr;
using PartyKing.Application.Queue.Services;
using PartyKing.Domain.Entities;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Tests;

public class QueueServiceNextTests
{
    private const string HostId = "host-profile";

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeQueueRepository _queue = new();
    private readonly QueueService _service;
    private readonly Session _session;

    public QueueServiceNextTests()
    {
        // Advancing needs neither Spotify, YouTube nor host tokens
        _service = new QueueService(_queue, _sessions, null!, null!, null!);
        _session = _sessions.AddSession(HostId);
    }

    [Fact]
    public async Task GetNext_PassesTheSongThePlayerThinksIsPlaying()
    {
        _queue.AdvanceResult = new QueueItem(_session.Id, QueueItemSource.Youtube, "url", "Title", null, null, null) { Id = 9 };

        var result = await _service.GetNextAsync(_session.Id, HostId, 8, CancellationToken.None);

        Assert.Equal(9, result.Value.Id);
        Assert.Equal((_session.Id, (int?)8), Assert.Single(_queue.AdvanceCalls));
    }

    [Fact]
    public async Task GetNext_ByAnotherAccount_IsForbiddenAndAdvancesNothing()
    {
        var result = await _service.GetNextAsync(_session.Id, "someone-else", null, CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Empty(_queue.AdvanceCalls);
    }

    [Fact]
    public async Task GetNext_EmptyQueue_IsNotFound()
    {
        _queue.AdvanceResult = null;

        var result = await _service.GetNextAsync(_session.Id, HostId, 1, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }
}
