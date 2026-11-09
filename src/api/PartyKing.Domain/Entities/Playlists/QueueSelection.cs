using PartyKing.Domain.Entities.Queue;

namespace PartyKing.Domain.Entities.Playlists;

/// <summary>How many songs of a session one <see cref="QueueSection"/> holds.</summary>
public readonly record struct QueueSectionCount(QueueSection Section, int Count);

public interface IQueueSelection
{
    /// <summary>
    /// The songs that belong to any of the <paramref name="sections"/>, in the order they were queued, each song
    /// (by its URL) only once: the first time it was queued.
    /// </summary>
    IReadOnlyList<QueueItem> Select(IEnumerable<QueueItem> items, IReadOnlyCollection<QueueSection> sections);

    /// <summary>The number of songs in every section, before songs are de-duplicated.</summary>
    IReadOnlyList<QueueSectionCount> Count(IReadOnlyCollection<QueueItem> items);
}

/// <summary>
/// Picks the songs of a session that go into a saved playlist, by the specifications it is given (one per
/// <see cref="QueueSection"/>, registered in Application). A new section is one more specification; this class does
/// not change.
/// </summary>
public class QueueSelection : IQueueSelection
{
    private readonly IReadOnlyList<IQueueSectionSpecification> _specifications;

    /// <summary>
    /// Every <see cref="QueueSection"/> needs exactly one specification: a section without one would select nothing
    /// without anyone noticing, and two would count its songs twice.
    /// </summary>
    /// <exception cref="ArgumentException">A section has no specification, or more than one.</exception>
    public QueueSelection(IEnumerable<IQueueSectionSpecification> specifications)
    {
        _specifications = specifications.ToList();

        foreach (var section in Enum.GetValues<QueueSection>())
        {
            var count = _specifications.Count(x => x.Section == section);

            if (count != 1)
            {
                throw new ArgumentException(
                    $"Section {section} needs exactly one specification, but has {count}",
                    nameof(specifications));
            }
        }
    }

    public IReadOnlyList<QueueItem> Select(
        IEnumerable<QueueItem> items,
        IReadOnlyCollection<QueueSection> sections)
    {
        var wanted = _specifications.Where(x => sections.Contains(x.Section)).ToList();

        return items
            .Where(item => wanted.Any(x => x.IsSatisfiedBy(item)))
            .OrderBy(x => x.Id)
            .DistinctBy(x => x.Url)
            .ToList();
    }

    public IReadOnlyList<QueueSectionCount> Count(IReadOnlyCollection<QueueItem> items)
    {
        return _specifications
            .Select(x => new QueueSectionCount(x.Section, items.Count(x.IsSatisfiedBy)))
            .ToList();
    }
}
