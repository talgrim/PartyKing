using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Events.Queue;

namespace PartyKing.Domain.Tests;

public class SessionQueueTests
{
    private static readonly Guid SessionId = Guid.NewGuid();

    private static QueueItem Song(int id, QueueItemStatus status = QueueItemStatus.Queued, Guid? sessionId = null)
    {
        return new QueueItem(sessionId ?? SessionId, QueueItemSource.Spotify, $"spotify:track:{id}", $"Song {id}", null, null, null)
        {
            Id = id,
            Status = status
        };
    }

    [Fact]
    public void NewSong_StartsQueuedAndNotFromTheFallback()
    {
        var song = new QueueItem(SessionId, QueueItemSource.Youtube, "https://youtu.be/x", "Title", null, null, null);

        Assert.Equal(QueueItemStatus.Queued, song.Status);
        Assert.False(song.IsFallback);
    }

    [Fact]
    public void NewFallbackSong_IsMarkedAsFromTheFallback()
    {
        var song = new QueueItem(SessionId, QueueItemSource.Spotify, "spotify:track:1", "T", null, null, null, isFallback: true);

        Assert.True(song.IsFallback);
    }

    [Fact]
    public void Of_WithASongOfAnotherSession_Throws()
    {
        Assert.Throws<ArgumentException>(() => SessionQueue.Of(SessionId, [Song(1, sessionId: Guid.NewGuid())]));
    }

    [Fact]
    public void Enqueue_RaisesSongQueuedWithTheSong()
    {
        var queue = SessionQueue.Of(SessionId, []);
        var song = Song(0);

        var added = queue.Enqueue(song);

        Assert.Same(song, added);
        var raised = Assert.IsType<SongQueued>(Assert.Single(queue.DrainEvents()));
        Assert.Equal(SessionId, raised.SessionId);
        Assert.Same(song, raised.Song);
    }

    [Fact]
    public void Enqueue_WithASongOfAnotherSession_Throws()
    {
        var queue = SessionQueue.Of(SessionId, []);

        Assert.Throws<ArgumentException>(() => queue.Enqueue(Song(0, sessionId: Guid.NewGuid())));
    }

    [Fact]
    public void EnqueueIfNoneQueued_WithNothingWaiting_AddsTheSong()
    {
        var queue = SessionQueue.Of(SessionId, [Song(1, QueueItemStatus.Playing), Song(2, QueueItemStatus.Played)]);

        var added = queue.EnqueueIfNoneQueued(Song(0));

        Assert.NotNull(added);
        Assert.IsType<SongQueued>(Assert.Single(queue.DrainEvents()));
    }

    [Fact]
    public void EnqueueIfNoneQueued_WithASongWaiting_AddsNothingAndRaisesNothing()
    {
        var queue = SessionQueue.Of(SessionId, [Song(1)]);

        var added = queue.EnqueueIfNoneQueued(Song(0));

        Assert.Null(added);
        Assert.Empty(queue.DrainEvents());
    }

    [Fact]
    public void Advance_MarksTheCurrentSongPlayedAndStartsTheOldestWaiting()
    {
        var current = Song(1, QueueItemStatus.Playing);
        var older = Song(2);
        var newer = Song(3);
        var queue = SessionQueue.Of(SessionId, [newer, current, older]);

        var result = queue.Advance(currentItemId: 1);

        Assert.True(result.Advanced);
        Assert.Equal(1, result.PreviousSongId);
        Assert.Same(older, result.Playing);
        Assert.Equal(QueueItemStatus.Played, current.Status);
        Assert.Equal(QueueItemStatus.Playing, older.Status);
        Assert.Equal(QueueItemStatus.Queued, newer.Status);
        Assert.Equal(new QueueAdvanced(SessionId, 1, older), Assert.Single(queue.DrainEvents()));
    }

    [Fact]
    public void Advance_WithNothingLeftToStart_PlaysNothingAndStillRaisesTheEvent()
    {
        var current = Song(1, QueueItemStatus.Playing);
        var queue = SessionQueue.Of(SessionId, [current]);

        var result = queue.Advance(1);

        Assert.True(result.Advanced);
        Assert.Null(result.Playing);
        Assert.Equal(new QueueAdvanced(SessionId, 1, null), Assert.Single(queue.DrainEvents()));
    }

    [Fact]
    public void Advance_WithNothingPlayingAndNullExpected_StartsTheFirstSong()
    {
        var first = Song(4);
        var queue = SessionQueue.Of(SessionId, [first, Song(5)]);

        var result = queue.Advance(null);

        Assert.True(result.Advanced);
        Assert.Null(result.PreviousSongId);
        Assert.Same(first, result.Playing);
        Assert.Equal(new QueueAdvanced(SessionId, null, first), Assert.Single(queue.DrainEvents()));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(null)]
    public void Advance_WithAnOutdatedView_ChangesNothingAndReportsWhatPlays(int? currentItemId)
    {
        var current = Song(1, QueueItemStatus.Playing);
        var waiting = Song(2);
        var queue = SessionQueue.Of(SessionId, [current, waiting]);

        var result = queue.Advance(currentItemId);

        Assert.False(result.Advanced);
        Assert.Null(result.PreviousSongId);
        Assert.Same(current, result.Playing);
        Assert.Equal(QueueItemStatus.Playing, current.Status);
        Assert.Equal(QueueItemStatus.Queued, waiting.Status);
        Assert.Empty(queue.DrainEvents());
    }

    [Fact]
    public void Advance_WithSeveralSongsPlaying_FinishesAllOfThem()
    {
        var first = Song(1, QueueItemStatus.Playing);
        var second = Song(2, QueueItemStatus.Playing);
        var queue = SessionQueue.Of(SessionId, [second, first]);

        var result = queue.Advance(1);

        Assert.True(result.Advanced);
        Assert.Equal(QueueItemStatus.Played, first.Status);
        Assert.Equal(QueueItemStatus.Played, second.Status);
    }

    [Fact]
    public void Remove_OfAWaitingSong_ReturnsItAndRaisesSongRemoved()
    {
        var song = Song(2);
        var queue = SessionQueue.Of(SessionId, [Song(1, QueueItemStatus.Playing), song]);

        var removal = queue.Remove(2);

        Assert.Equal(QueueItemRemoval.Removed, removal.Outcome);
        Assert.Same(song, removal.Item);
        Assert.Equal(new SongRemoved(SessionId, 2), Assert.Single(queue.DrainEvents()));
    }

    [Fact]
    public void Remove_OfAWaitingSongTwice_FindsItGoneTheSecondTime()
    {
        var queue = SessionQueue.Of(SessionId, [Song(2)]);
        queue.Remove(2);
        queue.DrainEvents();

        var removal = queue.Remove(2);

        Assert.Equal(QueueItemRemoval.NotFound, removal.Outcome);
        Assert.Empty(queue.DrainEvents());
    }

    [Theory]
    [InlineData(QueueItemStatus.Playing)]
    [InlineData(QueueItemStatus.Played)]
    public void Remove_OfASongThatIsNotWaiting_LeavesItAndRaisesNothing(QueueItemStatus status)
    {
        var queue = SessionQueue.Of(SessionId, [Song(1, status)]);

        var removal = queue.Remove(1);

        Assert.Equal(QueueItemRemoval.NotQueued, removal.Outcome);
        Assert.Null(removal.Item);
        Assert.Empty(queue.DrainEvents());
    }

    [Fact]
    public void Remove_OfAnUnknownSong_IsNotFound()
    {
        var queue = SessionQueue.Of(SessionId, [Song(1)]);

        var removal = queue.Remove(42);

        Assert.Equal(QueueItemRemoval.NotFound, removal.Outcome);
        Assert.Null(removal.Item);
        Assert.Empty(queue.DrainEvents());
    }
}
