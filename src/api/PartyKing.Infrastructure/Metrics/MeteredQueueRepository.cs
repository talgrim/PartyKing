using PartyKing.Domain.Repositories;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Infrastructure.Metrics;

internal sealed class MeteredQueueRepository : MeteredRepository, IQueueRepository
{
    private readonly IQueueRepository _inner;

    public MeteredQueueRepository(IQueueRepository inner, RepositoryMetrics metrics)
        : base(nameof(QueueRepository), metrics)
    {
        _inner = inner;
    }

    public Task<QueueItem> AddAsync(QueueItem item, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(AddAsync), () => _inner.AddAsync(item, cancellationToken));
    }

    public Task<QueueItem?> AddIfNoneQueuedAsync(QueueItem item, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(AddIfNoneQueuedAsync), () => _inner.AddIfNoneQueuedAsync(item, cancellationToken));
    }

    public Task<IReadOnlyList<QueueItem>> GetPendingAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetPendingAsync), () => _inner.GetPendingAsync(sessionId, cancellationToken));
    }

    public Task<QueueItem?> GetPlayingAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetPlayingAsync), () => _inner.GetPlayingAsync(sessionId, cancellationToken));
    }

    public Task<QueueItem?> AdvanceAsync(Guid sessionId, int? currentItemId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(AdvanceAsync), () => _inner.AdvanceAsync(sessionId, currentItemId, cancellationToken));
    }

    public Task<QueueItemRemoval> RemoveAsync(Guid sessionId, int itemId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(RemoveAsync), () => _inner.RemoveAsync(sessionId, itemId, cancellationToken));
    }
}
