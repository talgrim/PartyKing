using PartyKing.Domain.Enums;
using PartyKing.Domain.Entities.Queue;

namespace PartyKing.Domain.Entities.Playlists;

public sealed class WaitingSongsSpecification : IQueueSectionSpecification
{
    public QueueSection Section => QueueSection.Queue;

    public bool IsSatisfiedBy(QueueItem item)
    {
        return item is { Status: QueueItemStatus.Queued, IsFallback: false };
    }
}
