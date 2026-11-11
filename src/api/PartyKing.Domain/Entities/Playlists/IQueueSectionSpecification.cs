using PartyKing.Domain.Entities.Queue;

namespace PartyKing.Domain.Entities.Playlists;

/// <summary>The rule that says which songs of a session belong to one <see cref="QueueSection"/>.</summary>
public interface IQueueSectionSpecification
{
    QueueSection Section { get; }

    bool IsSatisfiedBy(QueueItem item);
}
