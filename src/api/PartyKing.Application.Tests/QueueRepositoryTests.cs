using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities;
using PartyKing.Domain.Enums;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>
/// Runs against a real SQLite file: the contexts hardcode "Data Source=PartyKing.db", so each test class gets a
/// fresh working directory to put it in.
/// </summary>
public class QueueRepositoryTests : IAsyncLifetime
{
    private readonly string _originalDirectory = Environment.CurrentDirectory;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");
    private ServiceProvider _provider = null!;
    private QueueRepository _repository = null!;
    private Guid _sessionId;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        Environment.CurrentDirectory = _directory;

        _provider = new ServiceCollection()
            .AddDbContextFactory<WriterDbContext>()
            .AddDbContextFactory<ReaderDbContext>()
            .BuildServiceProvider();

        var writer = _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>();
        await using (var dbContext = await writer.CreateDbContextAsync())
        {
            await dbContext.Database.MigrateAsync();

            var session = new Session("ABC123", "Party");
            _sessionId = session.Id;
            dbContext.Sessions.Add(session);
            await dbContext.SaveChangesAsync();
        }

        _repository = new QueueRepository(
            _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>(),
            writer);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Environment.CurrentDirectory = _originalDirectory;
        Directory.Delete(_directory, recursive: true);
    }

    private async Task<int> AddSongAsync(string title)
    {
        var item = await _repository.AddAsync(
            new QueueItem(_sessionId, QueueItemSource.Spotify, $"spotify:track:{title}", title, null, null, null),
            CancellationToken.None);
        return item.Id;
    }

    private Task<QueueItem?> AdvanceAsync(int? currentItemId) =>
        _repository.AdvanceAsync(_sessionId, currentItemId, CancellationToken.None);

    private async Task<string[]> PendingTitlesAsync() =>
        (await _repository.GetPendingAsync(_sessionId, CancellationToken.None)).Select(x => x.Title).ToArray();

    [Fact]
    public async Task Advance_WhenNothingPlays_StartsTheFirstQueuedSong()
    {
        var first = await AddSongAsync("one");
        await AddSongAsync("two");

        var playing = await AdvanceAsync(null);

        Assert.Equal(first, playing?.Id);
        Assert.Equal(first, (await _repository.GetPlayingAsync(_sessionId, CancellationToken.None))?.Id);
    }

    [Fact]
    public async Task Advance_WhenTheCurrentSongMatches_MovesToTheNextOne()
    {
        var first = await AddSongAsync("one");
        var second = await AddSongAsync("two");
        await AdvanceAsync(null);

        var playing = await AdvanceAsync(first);

        Assert.Equal(second, playing?.Id);
        Assert.Equal(second, (await _repository.GetPlayingAsync(_sessionId, CancellationToken.None))?.Id);
    }

    [Fact]
    public async Task Advance_WhenTheCallerIsBehind_AdvancesNothingAndReturnsTheSongPlaying()
    {
        var first = await AddSongAsync("one");
        var second = await AddSongAsync("two");
        await AddSongAsync("three");
        await AdvanceAsync(null);
        await AdvanceAsync(first); // an admin skipped to "two"

        // The host's player still believes "one" is playing and reports that it ended
        var playing = await AdvanceAsync(first);

        Assert.Equal(second, playing?.Id);
        Assert.Equal(second, (await _repository.GetPlayingAsync(_sessionId, CancellationToken.None))?.Id);
        Assert.Equal(["three"], await PendingTitlesAsync());
    }

    [Fact]
    public async Task Advance_WhenTheCallerThinksNothingPlaysButSomethingDoes_AdvancesNothing()
    {
        var first = await AddSongAsync("one");
        await AddSongAsync("two");
        await AdvanceAsync(null);

        var playing = await AdvanceAsync(null);

        Assert.Equal(first, playing?.Id);
        Assert.Equal(["two"], await PendingTitlesAsync());
    }

    [Fact]
    public async Task Advance_PastTheLastSong_MarksItPlayedAndReturnsNull()
    {
        var first = await AddSongAsync("one");
        await AdvanceAsync(null);

        var playing = await AdvanceAsync(first);

        Assert.Null(playing);
        Assert.Null(await _repository.GetPlayingAsync(_sessionId, CancellationToken.None));
    }

    [Fact]
    public async Task Advance_WithAnEmptyQueue_ReturnsNull()
    {
        Assert.Null(await AdvanceAsync(null));
    }

    [Fact]
    public async Task GetPending_LeavesOutTheSongPlayingAndThosePlayed()
    {
        var first = await AddSongAsync("one");
        await AddSongAsync("two");
        await AddSongAsync("three");
        await AdvanceAsync(null);
        await AdvanceAsync(first);

        Assert.Equal(["three"], await PendingTitlesAsync());
    }

    [Fact]
    public async Task GetPlaying_WhenNothingPlays_ReturnsNull()
    {
        await AddSongAsync("one");

        Assert.Null(await _repository.GetPlayingAsync(_sessionId, CancellationToken.None));
    }
}
