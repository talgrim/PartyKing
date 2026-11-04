using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Lyrics;
using PartyKing.Domain.Events.Lyrics;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>What the lyrics offsets repository saves and dispatches, against a real SQLite file.</summary>
public class LyricsOffsetsRepositoryEventsTests : IAsyncLifetime
{
    private const string Url = "spotify:track:7";

    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly RecordingDispatcher _dispatcher = new();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");
    private readonly Guid _sessionId = Guid.NewGuid();
    private string ConnectionString => $"Data Source={Path.Combine(_directory, "PartyKing.db")}";
    private ServiceProvider _provider = null!;
    private LyricsOffsetsRepository _repository = null!;

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
        }

        _repository = new LyricsOffsetsRepository(
            _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>(), writer, _dispatcher);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private async Task<int> RowCountAsync()
    {
        var writer = _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>();
        await using var dbContext = await writer.CreateDbContextAsync();

        return await dbContext.LyricsOffsets.CountAsync();
    }

    [Fact]
    public async Task Get_WithoutASavedOffset_IsZero()
    {
        Assert.Equal(0, await _repository.GetAsync(Url, Ct));
    }

    [Fact]
    public async Task Change_CreatesTheRow_AndDispatchesAfterTheCommit()
    {
        var seenByHandlers = int.MinValue;
        _dispatcher.OnDispatch = async _ => seenByHandlers = await _repository.GetAsync(Url, Ct);

        var failure = await _repository.ChangeAsync(Url, 2000, _sessionId, 7, Ct);

        Assert.Equal(LyricsOffsetFailure.None, failure);
        Assert.Equal(2000, await _repository.GetAsync(Url, Ct));
        Assert.Equal(2000, seenByHandlers);
        Assert.Equal(new LyricsOffsetChanged(_sessionId, 7, Url, 2000), Assert.Single(_dispatcher.Events));
    }

    [Fact]
    public async Task Change_OfASavedSong_UpdatesTheRow()
    {
        await _repository.ChangeAsync(Url, 2000, _sessionId, 7, Ct);

        await _repository.ChangeAsync(Url, -300, _sessionId, 7, Ct);

        Assert.Equal(-300, await _repository.GetAsync(Url, Ct));
        Assert.Equal(1, await RowCountAsync());
        Assert.Equal(2, _dispatcher.Events.Count);
    }

    [Fact]
    public async Task Change_ToTheSavedValue_StillDispatches()
    {
        await _repository.ChangeAsync(Url, 2000, _sessionId, 7, Ct);

        await _repository.ChangeAsync(Url, 2000, _sessionId, 7, Ct);

        Assert.Equal(2, _dispatcher.Events.Count);
    }

    [Fact]
    public async Task Change_BeyondTheRange_SavesAndDispatchesNothing()
    {
        var failure = await _repository.ChangeAsync(Url, 100_001, _sessionId, 7, Ct);

        Assert.Equal(LyricsOffsetFailure.OutOfRange, failure);
        Assert.Equal(0, await RowCountAsync());
        Assert.Empty(_dispatcher.Events);
    }

    [Fact]
    public async Task Change_BeyondTheRange_KeepsTheSavedOffset()
    {
        await _repository.ChangeAsync(Url, 2000, _sessionId, 7, Ct);
        _dispatcher.Events.Clear();

        await _repository.ChangeAsync(Url, -100_001, _sessionId, 7, Ct);

        Assert.Equal(2000, await _repository.GetAsync(Url, Ct));
        Assert.Empty(_dispatcher.Events);
    }

    [Fact]
    public async Task Change_RacingForANewSong_AllSucceedAndLeaveOneRow()
    {
        var saves = Enumerable.Range(1, 8)
            .Select(i => Task.Run(() => _repository.ChangeAsync(Url, i * 100, _sessionId, 7, Ct)))
            .ToArray();

        var failures = await Task.WhenAll(saves);

        Assert.All(failures, failure => Assert.Equal(LyricsOffsetFailure.None, failure));
        Assert.Equal(1, await RowCountAsync());
        Assert.Equal(8, _dispatcher.Events.Count);
        Assert.Contains(await _repository.GetAsync(Url, Ct), Enumerable.Range(1, 8).Select(i => i * 100));
    }
}
