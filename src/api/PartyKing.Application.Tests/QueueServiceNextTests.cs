using Microsoft.Extensions.Logging.Abstractions;
using ErrorOr;
using PartyKing.Application.Mappings;
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
    private readonly FakeQueueAdvancer _advancer = new();
    private readonly QueueService _service;
    private readonly Session _session;

    public QueueServiceNextTests()
    {
        // Advancing needs neither Spotify, YouTube nor host tokens
        _service = new QueueService(_queue, _sessions, null!, null!, null!, _advancer, NullLogger<QueueService>.Instance);
        _session = _sessions.AddSession(HostId);
    }

    [Fact]
    public async Task GetNext_PassesTheSongThePlayerThinksIsPlaying()
    {
        _advancer.Result = new QueueItem(_session.Id, QueueItemSource.Youtube, "url", "Title", null, null, null) { Id = 9 }.ToDto();

        var result = await _service.GetNextAsync(_session.Id, HostId, 8, CancellationToken.None);

        Assert.Equal(9, result.Value.Id);
        Assert.Equal((_session.Id, (int?)8), Assert.Single(_advancer.Calls));
    }

    [Fact]
    public async Task GetNext_ByAnotherAccount_IsForbiddenAndAdvancesNothing()
    {
        var result = await _service.GetNextAsync(_session.Id, "someone-else", null, CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Empty(_advancer.Calls);
    }

    [Fact]
    public async Task GetNext_FromAMissingSession_IsNotFoundAndAdvancesNothing()
    {
        var result = await _service.GetNextAsync(Guid.NewGuid(), HostId, null, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Empty(_advancer.Calls);
    }

    [Fact]
    public async Task GetNext_ReturnsWhatTheAdvancerAnswers_AnEmptyQueueIncluded()
    {
        var result = await _service.GetNextAsync(_session.Id, HostId, 1, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }
}
