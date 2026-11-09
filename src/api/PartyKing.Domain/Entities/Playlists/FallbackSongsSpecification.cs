using PartyKing.Domain.Entities.Queue;

namespace PartyKing.Domain.Entities.Playlists;

public sealed class FallbackSongsSpecification : IQueueSectionSpecification
{
    public QueueSection Section => QueueSection.Fallback;

    public bool IsSatisfiedBy(QueueItem item)
    {
        return item.IsFallback;
    }
}
