using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Ports;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Entities.Slideshow;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>Which photo the slideshow shows next and what it remembers of the ones it left, against a real SQLite file.</summary>
public class SlideshowImagesRepositoryTests : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly TimeSpan SlideTime = TimeSpan.FromSeconds(5);

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

            var session = new Session(Guid.NewGuid(), "ABC123", "Party", "hash", DateTime.UtcNow);
            _sessionId = session.Id;
            dbContext.Sessions.Add(session);
            await dbContext.SaveChangesAsync();
        }

        _repository = new SlideshowImagesRepository(
            _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>(),
            writer,
            NullLogger<SlideshowImagesRepository>.Instance,
            new RecordingDispatcher());
        await _repository.InitializeAsync(_directory, "Images/Uploaded", Ct);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private static SlideshowSettings Settings(bool autoRepeat = true, bool startFromBeginning = true)
    {
        var settings = SlideshowSettings.Defaults(Guid.NewGuid(), SlideTime);
        settings.Change(autoRepeat, startFromBeginning, SlideTime, SlideTime);

        return settings;
    }

    private async Task<List<Guid>> UploadAsync(int count, bool deleteAfterPresentation = false)
    {
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            await _repository.AddSlideshowImageAsync(
                new SlideshowImageWriteModel(
                    _sessionId,
                    ".jpg",
                    "image/jpeg",
                    new MemoryStream([1, 2, 3]),
                    $"Images/Uploaded/{_sessionId}",
                    deleteAfterPresentation),
                Ct);
            // GUIDv7 carries the milliseconds: uploads in the same one may not sort in upload order
            await Task.Delay(2);
        }

        var writer = _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>();
        await using var dbContext = await writer.CreateDbContextAsync();
        ids.AddRange((await dbContext.Images.Select(x => x.Id).ToListAsync()).OrderBy(x => x.ToString(), StringComparer.Ordinal));

        return ids;
    }

    private async Task<bool> WasPresentedAsync(Guid imageId)
    {
        var writer = _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>();
        await using var dbContext = await writer.CreateDbContextAsync();

        return (await dbContext.Images.SingleAsync(x => x.Id == imageId)).WasPresented;
    }

    [Fact]
    public async Task Get_WithoutPhotos_HasNoImageAndKeepsTheCursor()
    {
        var cursor = Guid.NewGuid();

        var result = await _repository.GetSlideshowImageAsync(_sessionId, cursor, Settings(), Ct);

        Assert.Null(result.Data);
        Assert.Equal(cursor, result.ImageId);
    }

    [Fact]
    public async Task Get_FromTheStart_ShowsTheFirstUploadedPhoto()
    {
        var ids = await UploadAsync(3);

        var result = await _repository.GetSlideshowImageAsync(_sessionId, null, Settings(), Ct);

        Assert.Equal(ids[0], result.ImageId);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task Get_ShowsThePhotosInUploadOrder_AndMarksTheOnesItLeft()
    {
        var ids = await UploadAsync(3);

        var second = await _repository.GetSlideshowImageAsync(_sessionId, ids[0], Settings(), Ct);

        Assert.Equal(ids[1], second.ImageId);
        Assert.True(await WasPresentedAsync(ids[0]));
        Assert.False(await WasPresentedAsync(ids[1]));
    }

    [Fact]
    public async Task Get_AtTheEnd_StartsOverWithAutoRepeat()
    {
        var ids = await UploadAsync(2);

        var result = await _repository.GetSlideshowImageAsync(_sessionId, ids[1], Settings(autoRepeat: true), Ct);

        Assert.Equal(ids[0], result.ImageId);
    }

    [Fact]
    public async Task Get_AtTheEnd_StopsWithoutAutoRepeat_KeepingTheCursor()
    {
        var ids = await UploadAsync(2);

        var result = await _repository.GetSlideshowImageAsync(_sessionId, ids[1], Settings(autoRepeat: false), Ct);

        Assert.Null(result.Data);
        Assert.Equal(ids[1], result.ImageId);
    }

    [Fact]
    public async Task Get_WithoutStartingFromTheBeginning_SkipsPhotosAlreadyPresented()
    {
        var ids = await UploadAsync(3);
        var settings = Settings(autoRepeat: true, startFromBeginning: false);

        await _repository.GetSlideshowImageAsync(_sessionId, ids[1], settings, Ct);
        var result = await _repository.GetSlideshowImageAsync(_sessionId, ids[0], settings, Ct);

        // 0 was just presented, 1 before it: only 2 is left
        Assert.Equal(ids[2], result.ImageId);
    }

    [Fact]
    public async Task Get_FromAPhotoThatWasDeleted_ContinuesWithTheNextOne()
    {
        var ids = await UploadAsync(3);
        var current = await _repository.GetSlideshowImageAsync(_sessionId, ids[0], Settings(), Ct);
        await _repository.DeleteImageAsync(current, Ct);

        var result = await _repository.GetSlideshowImageAsync(_sessionId, current.ImageId, Settings(), Ct);

        Assert.Equal(ids[2], result.ImageId);
    }

    [Fact]
    public async Task Get_IgnoresThePhotosOfOtherSessions()
    {
        await UploadAsync(1);

        var result = await _repository.GetSlideshowImageAsync(Guid.NewGuid(), null, Settings(), Ct);

        Assert.Null(result.Data);
    }
}
