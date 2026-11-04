using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Entities.Slideshow;
using PartyKing.Domain.Events.Slideshow;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>What the slideshow settings writes save and dispatch, against a real SQLite file.</summary>
public class SlideshowSettingsRepositoryEventsTests : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly TimeSpan DefaultSlideTime = TimeSpan.FromSeconds(5);

    private readonly RecordingDispatcher _dispatcher = new();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");
    private string ConnectionString => $"Data Source={Path.Combine(_directory, "PartyKing.db")}";
    private ServiceProvider _provider = null!;
    private SlideshowImagesRepository _repository = null!;
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

            // The settings row has a foreign key to its session
            var session = new Session(Guid.NewGuid(), "ABC123", "Party", "hash", DateTime.UtcNow);
            _sessionId = session.Id;
            dbContext.Sessions.Add(session);
            await dbContext.SaveChangesAsync();
        }

        _repository = new SlideshowImagesRepository(
            _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>(),
            writer,
            NullLogger<SlideshowImagesRepository>.Instance,
            _dispatcher);
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

        return await dbContext.SlideshowSettings.CountAsync();
    }

    private Task<SlideshowSettingsFailure> ChangeAsync(bool autoRepeat, bool startFromBeginning, TimeSpan? slideTime)
    {
        return _repository.ChangeAsync(_sessionId, autoRepeat, startFromBeginning, slideTime, DefaultSlideTime, Ct);
    }

    [Fact]
    public async Task Get_OfASessionThatNeverChangedThem_IsNull()
    {
        Assert.Null(await _repository.GetSettingsAsync(_sessionId, Ct));
    }

    [Fact]
    public async Task Change_CreatesTheRow_AndDispatchesAfterTheCommit()
    {
        SlideshowSettings? seenByHandlers = null;
        _dispatcher.OnDispatch = async _ => seenByHandlers = await _repository.GetSettingsAsync(_sessionId, Ct);

        var failure = await ChangeAsync(false, true, TimeSpan.FromSeconds(30));

        Assert.Equal(SlideshowSettingsFailure.None, failure);
        var saved = await _repository.GetSettingsAsync(_sessionId, Ct);
        Assert.False(saved!.AutoRepeat);
        Assert.True(saved.StartFromBeginning);
        Assert.Equal(TimeSpan.FromSeconds(30), saved.SlideTime);
        Assert.Equal(TimeSpan.FromSeconds(30), seenByHandlers!.SlideTime);
        Assert.Equal(
            new SlideshowSettingsChanged(_sessionId, false, true, TimeSpan.FromSeconds(30)),
            Assert.Single(_dispatcher.Events));
    }

    [Fact]
    public async Task Change_WithoutASlideTime_StoresTheDefault()
    {
        await ChangeAsync(true, true, null);

        Assert.Equal(DefaultSlideTime, (await _repository.GetSettingsAsync(_sessionId, Ct))!.SlideTime);
    }

    [Fact]
    public async Task Change_OfASessionWithSettings_UpdatesTheRow()
    {
        await ChangeAsync(true, true, TimeSpan.FromSeconds(30));

        await ChangeAsync(false, false, TimeSpan.FromSeconds(10));

        var saved = await _repository.GetSettingsAsync(_sessionId, Ct);
        Assert.False(saved!.AutoRepeat);
        Assert.False(saved.StartFromBeginning);
        Assert.Equal(TimeSpan.FromSeconds(10), saved.SlideTime);
        Assert.Equal(1, await RowCountAsync());
        Assert.Equal(2, _dispatcher.Events.Count);
    }

    [Fact]
    public async Task Change_ToTheSavedValues_StillDispatches()
    {
        await ChangeAsync(true, true, TimeSpan.FromSeconds(30));

        await ChangeAsync(true, true, TimeSpan.FromSeconds(30));

        Assert.Equal(2, _dispatcher.Events.Count);
    }

    [Fact]
    public async Task Change_WithARefusedSlideTime_SavesAndDispatchesNothing()
    {
        var failure = await ChangeAsync(true, true, TimeSpan.FromHours(2));

        Assert.Equal(SlideshowSettingsFailure.SlideTimeOutOfRange, failure);
        Assert.Equal(0, await RowCountAsync());
        Assert.Empty(_dispatcher.Events);
    }

    [Fact]
    public async Task Change_RacingForANewSession_AllSucceedAndLeaveOneRow()
    {
        var saves = Enumerable.Range(1, 8)
            .Select(i => Task.Run(() => ChangeAsync(true, true, TimeSpan.FromSeconds(i))))
            .ToArray();

        var failures = await Task.WhenAll(saves);

        Assert.All(failures, failure => Assert.Equal(SlideshowSettingsFailure.None, failure));
        Assert.Equal(1, await RowCountAsync());
        Assert.Equal(8, _dispatcher.Events.Count);
    }
}
