using PartyKing.Domain.Entities.Playlists;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Enums;

namespace PartyKing.Domain.Tests;

public class QueueSelectionTests
{
    private static readonly Guid SessionId = Guid.NewGuid();

    private static readonly QueueSelection Selection = new(
    [
        new PlayedSongsSpecification(),
        new PlayingSongSpecification(),
        new WaitingSongsSpecification(),
        new FallbackSongsSpecification()
    ]);

    private static QueueItem Song(
        int id,
        QueueItemStatus status,
        bool isFallback = false,
        string? url = null)
    {
        return new QueueItem(
            SessionId,
            QueueItemSource.Spotify,
            url ?? $"spotify:track:{id}",
            $"Song {id}",
            null,
            null,
            null,
            isFallback)
        {
            Id = id,
            Status = status
        };
    }

    /// <summary>One song of every kind: played 1, playing 2, waiting 3, and the fallback ones played 4, playing 5, waiting 6.</summary>
    private static List<QueueItem> ASession()
    {
        return
        [
            Song(1, QueueItemStatus.Played),
            Song(2, QueueItemStatus.Playing),
            Song(3, QueueItemStatus.Queued),
            Song(4, QueueItemStatus.Played, isFallback: true),
            Song(5, QueueItemStatus.Playing, isFallback: true),
            Song(6, QueueItemStatus.Queued, isFallback: true)
        ];
    }

    private static int[] Ids(IEnumerable<QueueItem> items) => items.Select(x => x.Id).ToArray();

    [Theory]
    [InlineData(QueueSection.History, new[] { 1 })]
    [InlineData(QueueSection.Current, new[] { 2 })]
    [InlineData(QueueSection.Queue, new[] { 3 })]
    [InlineData(QueueSection.Fallback, new[] { 4, 5, 6 })]
    public void ASection_TakesOnlyItsOwnSongs(QueueSection section, int[] expected)
    {
        Assert.Equal(expected, Ids(Selection.Select(ASession(), [section])));
    }

    [Fact]
    public void SeveralSections_AreUnitedInTheOrderTheSongsWereQueued()
    {
        var selected = Selection.Select(ASession(), [QueueSection.Queue, QueueSection.History, QueueSection.Fallback]);

        Assert.Equal([1, 3, 4, 5, 6], Ids(selected));
    }

    [Fact]
    public void WithoutSections_NothingIsSelected()
    {
        Assert.Empty(Selection.Select(ASession(), []));
    }

    [Fact]
    public void ASongQueuedTwice_IsKeptOnlyTheFirstTime()
    {
        var items = new List<QueueItem>
        {
            Song(1, QueueItemStatus.Played, url: "spotify:track:same"),
            Song(2, QueueItemStatus.Playing),
            Song(3, QueueItemStatus.Queued, url: "spotify:track:same")
        };

        var selected = Selection.Select(items, [QueueSection.History, QueueSection.Current, QueueSection.Queue]);

        Assert.Equal([1, 2], Ids(selected));
    }

    [Fact]
    public void ASongInTwoChosenSections_IsKeptOnce()
    {
        var items = new List<QueueItem>
        {
            Song(1, QueueItemStatus.Played, url: "spotify:track:same"),
            Song(2, QueueItemStatus.Queued, isFallback: true, url: "spotify:track:same")
        };

        var selected = Selection.Select(items, [QueueSection.History, QueueSection.Fallback]);

        Assert.Equal([1], Ids(selected));
    }

    [Fact]
    public void ItemsGivenOutOfOrder_AreReturnedInQueueOrder()
    {
        var items = new List<QueueItem> { Song(3, QueueItemStatus.Queued), Song(1, QueueItemStatus.Queued) };

        Assert.Equal([1, 3], Ids(Selection.Select(items, [QueueSection.Queue])));
    }

    [Fact]
    public void EverySongOfASession_IsInExactlyOneSection()
    {
        var items = ASession();

        var counts = Selection.Count(items);

        Assert.Equal(items.Count, counts.Sum(x => x.Count));
    }

    [Fact]
    public void Count_ListsEverySectionWithItsNumberOfSongs()
    {
        var counts = Selection.Count(ASession());

        Assert.Equal(
            [
                new QueueSectionCount(QueueSection.History, 1),
                new QueueSectionCount(QueueSection.Current, 1),
                new QueueSectionCount(QueueSection.Queue, 1),
                new QueueSectionCount(QueueSection.Fallback, 3)
            ],
            counts);
    }

    [Fact]
    public void Count_OfASessionWithoutSongs_ListsEverySectionAsEmpty()
    {
        var counts = Selection.Count([]);

        Assert.Equal(Enum.GetValues<QueueSection>().Length, counts.Count);
        Assert.All(counts, x => Assert.Equal(0, x.Count));
    }

    [Fact]
    public void Count_CountsDuplicatesInEachSection()
    {
        var items = new List<QueueItem>
        {
            Song(1, QueueItemStatus.Queued, url: "spotify:track:same"),
            Song(2, QueueItemStatus.Queued, url: "spotify:track:same")
        };

        Assert.Equal(2, Selection.Count(items).Single(x => x.Section == QueueSection.Queue).Count);
    }

    [Fact]
    public void WithoutASpecificationForASection_ItCannotBeBuilt()
    {
        var withoutFallback = new IQueueSectionSpecification[]
        {
            new PlayedSongsSpecification(),
            new PlayingSongSpecification(),
            new WaitingSongsSpecification()
        };

        var exception = Assert.Throws<ArgumentException>(() => new QueueSelection(withoutFallback));

        Assert.Contains("Fallback", exception.Message);
    }

    [Fact]
    public void WithTwoSpecificationsForASection_ItCannotBeBuilt()
    {
        var twice = new IQueueSectionSpecification[]
        {
            new PlayedSongsSpecification(),
            new PlayedSongsSpecification(),
            new PlayingSongSpecification(),
            new WaitingSongsSpecification(),
            new FallbackSongsSpecification()
        };

        var exception = Assert.Throws<ArgumentException>(() => new QueueSelection(twice));

        Assert.Contains("History", exception.Message);
    }
}
