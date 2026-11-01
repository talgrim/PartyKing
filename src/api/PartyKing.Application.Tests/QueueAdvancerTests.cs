using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Queue.Services;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Tests;

public class QueueAdvancerTests
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly FakeQueueRepository _queue = new();
    private readonly FakeFallbackQueueFiller _filler = new();
    private readonly QueueAdvancer _advancer;

    public QueueAdvancerTests()
    {
        _advancer = new QueueAdvancer(_queue, _filler, NullLogger<QueueAdvancer>.Instance);
    }

    private QueueItem Song(int id) =>
        new(_sessionId, QueueItemSource.Spotify, $"spotify:track:{id}", $"Song {id}", null, null, null) { Id = id };

    [Fact]
    public async Task Advance_PassesTheSongTheCallerThinksIsPlaying()
    {
        _queue.AdvanceResult = Song(2);

        var result = await _advancer.AdvanceAsync(_sessionId, 1, CancellationToken.None);

        Assert.Equal(2, result.Value.Id);
        Assert.Equal((_sessionId, (int?)1), Assert.Single(_queue.AdvanceCalls));
    }

    [Fact]
    public async Task Advance_TopsUpTheQueueBeforeAdvancing_SoTheAdvanceFindsASong()
    {
        var ensuredWhenAdvancing = -1;
        _queue.OnAdvance = () => ensuredWhenAdvancing = _filler.Ensured.Count;
        _queue.AdvanceResult = Song(2);

        await _advancer.AdvanceAsync(_sessionId, 1, CancellationToken.None);

        Assert.Equal(1, ensuredWhenAdvancing);
        // The top-up after the advance is the refill handler's, see FallbackRefillHandlerTests
        Assert.Equal(_sessionId, Assert.Single(_filler.Ensured));
    }

    [Fact]
    public async Task Advance_WhenNothingIsLeft_IsNotFound()
    {
        _queue.AdvanceResult = null;

        var result = await _advancer.AdvanceAsync(_sessionId, 1, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task Advance_WithAnOutdatedSong_ReturnsTheSongThatIsReallyPlaying()
    {
        _queue.AdvanceMovesOn = false;
        _queue.AdvanceResult = Song(9);

        var result = await _advancer.AdvanceAsync(_sessionId, 8, CancellationToken.None);

        Assert.Equal(9, result.Value.Id);
    }
}
