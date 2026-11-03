using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Events.Sessions;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>
/// A claim or release that commits between a session's load and save must not be overwritten by the stale save: the
/// host is a concurrency token and the update runs again on a fresh load.
/// </summary>
public class SessionsRepositoryRaceTests : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly RecordingDispatcher _dispatcher = new();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");
    private readonly BeforeSaveInterceptor _interceptor = new();
    private string ConnectionString => $"Data Source={Path.Combine(_directory, "PartyKing.db")}";
    private ServiceProvider _provider = null!;
    private SessionsRepository _repository = null!;
    private Guid _sessionId;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        _provider = new ServiceCollection()
            .AddDbContextFactory<WriterDbContext>(options => options.UseSqlite(ConnectionString).AddInterceptors(_interceptor))
            .AddDbContextFactory<ReaderDbContext>(options => options.UseSqlite(ConnectionString))
            .BuildServiceProvider();

        var writer = _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>();
        await using (var dbContext = await writer.CreateDbContextAsync())
        {
            await dbContext.Database.MigrateAsync();

            dbContext.SpotifyProfiles.AddRange(new SpotifyProfile("host-1", "refresh-1"), new SpotifyProfile("host-2", "refresh-2"));

            var session = new Session("ABC123", "Party", "hash");
            _sessionId = session.Id;
            dbContext.Sessions.Add(session);
            await dbContext.SaveChangesAsync();
        }

        _repository = new SessionsRepository(
            _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>(), writer, _dispatcher);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private void SetHostBehindTheRepository(string? hostProfileId)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Sessions SET HostProfileId = $host WHERE Id = $id";
        command.Parameters.AddWithValue("$host", (object?)hostProfileId ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", _sessionId);
        command.ExecuteNonQuery();
    }

    private async Task<Session> ReadAsync()
    {
        var reader = _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>();
        await using var dbContext = await reader.CreateDbContextAsync();
        return await dbContext.Sessions.AsNoTracking().SingleAsync(x => x.Id == _sessionId);
    }

    [Fact]
    public async Task Release_AfterTheHostChangedUnderIt_ReleasesTheNewHost_AndNamesThem()
    {
        await _repository.TryClaimAsync(_sessionId, "host-1", Ct);
        _dispatcher.Events.Clear();
        // host-1 goes and host-2 claims after the release loaded host-1, but before it saved
        _interceptor.OnFirstSave = () =>
        {
            SetHostBehindTheRepository(null);
            SetHostBehindTheRepository("host-2");
        };

        var released = await _repository.ReleaseAsync(_sessionId, Ct);

        Assert.True(released);
        Assert.Null((await ReadAsync()).HostProfileId);
        Assert.Equal(new HostReleased(_sessionId, "host-2"), Assert.Single(_dispatcher.Events));
    }

    [Fact]
    public async Task Rename_AfterAClaimUnderIt_StillSucceeds_AndKeepsTheClaim()
    {
        _interceptor.OnFirstSave = () => SetHostBehindTheRepository("host-2");

        var renamed = await _repository.RenameAsync(_sessionId, "Renamed", Ct);

        Assert.True(renamed);
        var session = await ReadAsync();
        Assert.Equal("Renamed", session.Name);
        Assert.Equal("host-2", session.HostProfileId);
    }

    [Fact]
    public async Task Rename_OfASessionDeletedUnderIt_AnswersFalse()
    {
        _interceptor.OnFirstSave = () =>
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Sessions";
            command.ExecuteNonQuery();
        };

        var renamed = await _repository.RenameAsync(_sessionId, "Renamed", Ct);

        Assert.False(renamed);
        Assert.Empty(_dispatcher.Events);
    }

    [Fact]
    public async Task Release_WhenTheHostKeepsChanging_ThrowsAfterThreeAttempts_AndDispatchesNothing()
    {
        await _repository.TryClaimAsync(_sessionId, "host-1", Ct);
        _dispatcher.Events.Clear();
        var toggle = false;
        _interceptor.OnEverySave = () =>
        {
            toggle = !toggle;
            SetHostBehindTheRepository(toggle ? "host-2" : "host-1");
        };

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => _repository.ReleaseAsync(_sessionId, Ct));

        Assert.Empty(_dispatcher.Events);
    }

    private sealed class BeforeSaveInterceptor : SaveChangesInterceptor
    {
        public Action? OnFirstSave { get; set; }
        public Action? OnEverySave { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var first = OnFirstSave;
            OnFirstSave = null;
            first?.Invoke();
            OnEverySave?.Invoke();
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
