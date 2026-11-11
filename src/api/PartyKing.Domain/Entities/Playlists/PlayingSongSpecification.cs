using PartyKing.Domain.Enums;
using PartyKing.Domain.Entities.Queue;

namespace PartyKing.Domain.Entities.Playlists;

public sealed class PlayingSongSpecification : IQueueSectionSpecification
{
    public QueueSection Section => QueueSection.Current;

    public bool IsSatisfiedBy(QueueItem item)
    {
        return item is { Status: QueueItemStatus.Playing, IsFallback: false };
    }
}
