using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Queue;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Events.Queue;
using PartyKing.Infrastructure.Metrics;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>
/// What the queue repository dispatches, against a real SQLite file: one event per saved change, once the change is
/// committed and the session's lock is free, and nothing for a call that changed nothing.
/// </summary>
public class QueueRepositoryEventsTests : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly RepositoryMetrics _metrics = new();
    private readonly RecordingDispatcher _dispatcher = new();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");
    private string ConnectionString => $"Data Source={Path.Combine(_directory, "PartyKing.db")}";
    private ServiceProvider _provider = null!;
    private QueueRepository _repository = null!;
    private Guid _sessionId;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        _provider = new ServiceCollection()
            .AddDbContextFactory<WriterDbContext>(options => options.UseSqlite(ConnectionString))
            .AddDbContextFactory<ReaderDbContext>(options => options.UseSqlite(ConnectionString))
            .BuildServiceProvider();

        var writer = _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>();
        await using (var dbContext = await writer.CreateDbContextAsync())
        {
            await dbContext.Database.MigrateAsync();

            var session = new Session("ABC123", "Party", "hash");
            _sessionId = session.Id;
            dbContext.Sessions.Add(session);
            await dbContext.SaveChangesAsync();
        }

        _repository = new QueueRepository(
            _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>(), writer, _metrics, _dispatcher);
    }

    public async Task DisposeAsync()
    {
        _metrics.Dispose();
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private QueueItem NewSong(string title) =>
        new(_sessionId, QueueItemSource.Spotify, $"spotify:track:{title}", title, null, null, null);

    private async Task<int> AddSongAsync(string title) => (await _repository.AddAsync(NewSong(title), Ct)).Id;

    [Fact]
    public async Task Add_DispatchesSongQueuedWithTheSavedId()
    {
        var id = await AddSongAsync("A");

        var queued = Assert.IsType<SongQueued>(Assert.Single(_dispatcher.Events));
        Assert.Equal(_sessionId, queued.SessionId);
        Assert.Equal(id, queued.Song.Id);
        Assert.NotEqual(0, queued.Song.Id);
    }

    [Fact]
    public async Task AddIfNoneQueued_DispatchesSongQueuedOnlyWhenItAdds()
    {
        var added = await _repository.AddIfNoneQueuedAsync(NewSong("A"), Ct);
        var refused = await _repository.AddIfNoneQueuedAsync(NewSong("B"), Ct);

        Assert.NotNull(added);
        Assert.Null(refused);
        var queued = Assert.IsType<SongQueued>(Assert.Single(_dispatcher.Events));
        Assert.Equal(added.Id, queued.Song.Id);
    }

    [Fact]
    public async Task Advance_DispatchesQueueAdvancedWithThePreviousAndTheNewSong()
    {
        var first = await AddSongAsync("A");
        var second = await AddSongAsync("B");
        await _repository.AdvanceAsync(_sessionId, null, Ct);
        _dispatcher.Events.Clear();

        var result = await _repository.AdvanceAsync(_sessionId, first, Ct);

        Assert.True(result.Advanced);
        Assert.Equal(first, result.PreviousSongId);
        Assert.Equal(second, result.Playing?.Id);
        var advanced = Assert.IsType<QueueAdvanced>(Assert.Single(_dispatcher.Events));
        Assert.Equal(first, advanced.PreviousSongId);
        Assert.Equal(second, advanced.NowPlaying?.Id);
        Assert.Equal(QueueItemStatus.Playing, advanced.NowPlaying?.Status);
    }

    [Fact]
    public async Task Advance_PastTheLastSong_DispatchesQueueAdvancedWithNoNowPlaying()
    {
        var only = await AddSongAsync("A");
        await _repository.AdvanceAsync(_sessionId, null, Ct);
        _dispatcher.Events.Clear();

        var result = await _repository.AdvanceAsync(_sessionId, only, Ct);

        Assert.True(result.Advanced);
        Assert.Null(result.Playing);
        var advanced = Assert.IsType<QueueAdvanced>(Assert.Single(_dispatcher.Events));
        Assert.Equal(only, advanced.PreviousSongId);
        Assert.Null(advanced.NowPlaying);
    }

    [Fact]
    public async Task Advance_WithAnOutdatedSong_ChangesAndDispatchesNothing()
    {
        var first = await AddSongAsync("A");
        await AddSongAsync("B");
        await _repository.AdvanceAsync(_sessionId, null, Ct);
        _dispatcher.Events.Clear();

        var result = await _repository.AdvanceAsync(_sessionId, first + 100, Ct);

        Assert.False(result.Advanced);
        Assert.Null(result.PreviousSongId);
        Assert.Equal(first, result.Playing?.Id);
        Assert.Empty(_dispatcher.Events);
    }

    [Fact]
    public async Task Remove_OfAnUnknownSong_DispatchesNothing()
    {
        var result = await _repository.RemoveAsync(_sessionId, 12345, Ct);

        Assert.Equal(QueueItemRemoval.NotFound, result);
        Assert.Empty(_dispatcher.Events);
    }

    [Fact]
    public async Task Remove_OfAWaitingSong_DispatchesSongRemoved()
    {
        await AddSongAsync("A");
        var waiting = await AddSongAsync("B");
        await _repository.AdvanceAsync(_sessionId, null, Ct);
        _dispatcher.Events.Clear();

        var result = await _repository.RemoveAsync(_sessionId, waiting, Ct);

        Assert.Equal(QueueItemRemoval.Removed, result);
        var removed = Assert.IsType<SongRemoved>(Assert.Single(_dispatcher.Events));
        Assert.Equal((_sessionId, waiting), (removed.SessionId, removed.SongId));
    }

    [Fact]
    public async Task Remove_OfThePlayingSong_DispatchesNothing()
    {
        await AddSongAsync("A");
        var playing = (await _repository.AdvanceAsync(_sessionId, null, Ct)).Playing!.Id;
        _dispatcher.Events.Clear();

        var result = await _repository.RemoveAsync(_sessionId, playing, Ct);

        Assert.Equal(QueueItemRemoval.NotQueued, result);
        Assert.Empty(_dispatcher.Events);
    }

    [Fact]
    public async Task Events_AreDispatchedAfterTheChangeIsCommitted()
    {
        await AddSongAsync("A");
        QueueItem? seenWhenDispatched = null;
        _dispatcher.OnDispatch = async _ =>
            seenWhenDispatched = await _repository.GetPlayingAsync(_sessionId, Ct);

        await _repository.AdvanceAsync(_sessionId, null, Ct);

        Assert.Equal(QueueItemStatus.Playing, seenWhenDispatched?.Status);
    }

    [Fact]
    public async Task Events_AreDispatchedAfterTheSessionLockIsReleased_SoAHandlerMayWriteAgain()
    {
        await AddSongAsync("A");
        _dispatcher.Events.Clear();
        _dispatcher.OnDispatch = async domainEvent =>
        {
            if (domainEvent is QueueAdvanced)
            {
                // What the refill handler does; it would wait for the lock forever if the repository still held it
                var added = await _repository
                    .AddIfNoneQueuedAsync(NewSong("Refill"), Ct)
                    .WaitAsync(TimeSpan.FromSeconds(5));
                Assert.NotNull(added);
            }
        };

        await _repository.AdvanceAsync(_sessionId, null, Ct);

        Assert.Contains(_dispatcher.Events, x => x is SongQueued);
    }
}
