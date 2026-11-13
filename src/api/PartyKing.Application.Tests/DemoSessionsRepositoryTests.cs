using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Demo;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>The demo sessions repository against a real SQLite file: the markers, the sweep's query and the cascade.</summary>
public class DemoSessionsRepositoryTests : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");
    private string ConnectionString => $"Data Source={Path.Combine(_directory, "PartyKing.db")}";
    private ServiceProvider _provider = null!;
    private DemoSessionsRepository _repository = null!;
    private int _codes;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        _provider = new ServiceCollection()
            .AddDbContextFactory<WriterDbContext>(options => options.UseSqlite(ConnectionString))
            .AddDbContextFactory<ReaderDbContext>(options => options.UseSqlite(ConnectionString))
            .BuildServiceProvider();

        await using var dbContext = await Writer.CreateDbContextAsync();
        await dbContext.Database.MigrateAsync();

        _repository = new DemoSessionsRepository(Reader, Writer);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private IDbContextFactory<WriterDbContext> Writer => _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>();
    private IDbContextFactory<ReaderDbContext> Reader => _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>();

    /// <summary>A session, which the marker has a foreign key to, with a demo marker that ends <paramref name="endsIn"/> after <see cref="Now"/>.</summary>
    private async Task<DemoSession> AddAsync(TimeSpan endsIn)
    {
        var session = new Session(Guid.NewGuid(), $"DEMO{_codes++}", "Party", "hash", DateTime.UtcNow);
        await using (var dbContext = await Writer.CreateDbContextAsync())
        {
            dbContext.Sessions.Add(session);
            await dbContext.SaveChangesAsync();
        }

        var demoSession = DemoSession.Start(session.Id, $"demo-{session.Id:N}", Now, endsIn);
        await _repository.AddAsync(demoSession, Ct);

        return demoSession;
    }

    [Fact]
    public async Task ASavedMarker_IsReadBack()
    {
        var saved = await AddAsync(TimeSpan.FromHours(3));

        var read = await _repository.GetAsync(saved.SessionId, Ct);

        Assert.Equal(saved.SessionId, read!.SessionId);
        Assert.Equal(saved.HostProfileId, read.HostProfileId);
        Assert.Equal(saved.ExpiresAt, read.ExpiresAt);
    }

    [Fact]
    public async Task AnOrdinarySession_HasNoMarker()
    {
        Assert.Null(await _repository.GetAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Count_CountsEveryMarker_ExpiredOnesIncluded()
    {
        await AddAsync(TimeSpan.FromHours(3));
        await AddAsync(TimeSpan.FromHours(-1));

        Assert.Equal(2, await _repository.CountAsync(Ct));
    }

    [Fact]
    public async Task ListExpired_ReturnsTheOverSessions_TheOldestFirst_AndTheOnesThatEndExactlyNow()
    {
        var later = await AddAsync(TimeSpan.FromMinutes(-5));
        var earlier = await AddAsync(TimeSpan.FromMinutes(-30));
        var exactlyNow = await AddAsync(TimeSpan.Zero);
        await AddAsync(TimeSpan.FromMinutes(1));

        var expired = await _repository.ListExpiredAsync(Now, Ct);

        Assert.Equal(
            [earlier.SessionId, later.SessionId, exactlyNow.SessionId],
            expired.Select(x => x.SessionId));
    }

    [Fact]
    public async Task DeletingTheSession_TakesItsMarkerWithIt()
    {
        var saved = await AddAsync(TimeSpan.FromHours(3));

        await using (var dbContext = await Writer.CreateDbContextAsync())
        {
            await dbContext.Sessions.Where(x => x.Id == saved.SessionId).ExecuteDeleteAsync();
        }

        Assert.Null(await _repository.GetAsync(saved.SessionId, Ct));
        Assert.Equal(0, await _repository.CountAsync(Ct));
    }

    [Fact]
    public async Task AMarker_NeedsItsSession()
    {
        var orphan = DemoSession.Start(Guid.NewGuid(), "demo-orphan", Now, TimeSpan.FromHours(1));

        await Assert.ThrowsAsync<DbUpdateException>(() => _repository.AddAsync(orphan, Ct));
    }
}
