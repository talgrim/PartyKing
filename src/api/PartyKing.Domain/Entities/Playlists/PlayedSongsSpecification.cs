using PartyKing.Domain.Enums;
using PartyKing.Domain.Entities.Queue;

namespace PartyKing.Domain.Entities.Playlists;

public sealed class PlayedSongsSpecification : IQueueSectionSpecification
{
    public QueueSection Section => QueueSection.History;

    public bool IsSatisfiedBy(QueueItem item)
    {
        return item is { Status: QueueItemStatus.Played, IsFallback: false };
    }
}
