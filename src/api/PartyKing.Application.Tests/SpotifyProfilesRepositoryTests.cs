using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>Deleting a profile (the demo's synthetic hosts) against a real SQLite file.</summary>
public class SpotifyProfilesRepositoryTests : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");
    private string ConnectionString => $"Data Source={Path.Combine(_directory, "PartyKing.db")}";
    private ServiceProvider _provider = null!;
    private SpotifyProfilesRepository _repository = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        _provider = new ServiceCollection()
            .AddDbContextFactory<WriterDbContext>(options => options.UseSqlite(ConnectionString))
            .AddDbContextFactory<ReaderDbContext>(options => options.UseSqlite(ConnectionString))
            .BuildServiceProvider();

        await using var dbContext = await Writer.CreateDbContextAsync();
        await dbContext.Database.MigrateAsync();

        _repository = new SpotifyProfilesRepository(
            _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>(),
            Writer);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private IDbContextFactory<WriterDbContext> Writer => _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>();

    private Task AddProfileAsync(string id) =>
        _repository.CreateProfileAsync(new AuthenticationResult(id, "Host", "access", $"refresh-{id}", 3600), Ct);

    [Fact]
    public async Task DeletingAProfile_RemovesIt_AndOnlyIt()
    {
        await AddProfileAsync("profile-1");
        await AddProfileAsync("profile-2");

        await _repository.DeleteProfileAsync("profile-1", Ct);

        Assert.Null(await _repository.GetProfileAsync("profile-1", Ct));
        Assert.NotNull(await _repository.GetProfileAsync("profile-2", Ct));
    }

    [Fact]
    public async Task DeletingAnUnknownProfile_DoesNothing()
    {
        await AddProfileAsync("profile-1");

        await _repository.DeleteProfileAsync("nobody", Ct);

        Assert.NotNull(await _repository.GetProfileAsync("profile-1", Ct));
    }

    [Fact]
    public async Task ASessionTheProfileHosted_IsLeftWithoutAHost()
    {
        await AddProfileAsync("profile-1");
        var session = new Session(Guid.NewGuid(), "ABC123", "Party", "hash", DateTime.UtcNow) { HostProfileId = "profile-1" };
        await using (var dbContext = await Writer.CreateDbContextAsync())
        {
            dbContext.Sessions.Add(session);
            await dbContext.SaveChangesAsync();
        }

        await _repository.DeleteProfileAsync("profile-1", Ct);

        await using var read = await Writer.CreateDbContextAsync();
        var kept = await read.Sessions.AsNoTracking().SingleAsync(x => x.Id == session.Id);
        Assert.Null(kept.HostProfileId);
    }
}
