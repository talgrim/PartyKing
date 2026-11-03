using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Events.Sessions;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>
/// What the sessions repository dispatches, against a real SQLite file: one event per saved change, once the change is
/// committed, and nothing for a call that changed nothing.
/// </summary>
public class SessionsRepositoryEventsTests : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly RecordingDispatcher _dispatcher = new();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");
    private string ConnectionString => $"Data Source={Path.Combine(_directory, "PartyKing.db")}";
    private ServiceProvider _provider = null!;
    private SessionsRepository _repository = null!;
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
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private async Task<Session> ReadAsync()
    {
        var session = await _repository.GetAsync(_sessionId, Ct);
        return session!;
    }

    [Fact]
    public async Task Rename_SavesTheTrimmedNameAndDispatchesAfterTheCommit()
    {
        string? nameSeenByHandlers = null;
        _dispatcher.OnDispatch = async _ => nameSeenByHandlers = (await ReadAsync()).Name;

        var renamed = await _repository.RenameAsync(_sessionId, "  Friday  ", Ct);

        Assert.True(renamed);
        Assert.Equal("Friday", nameSeenByHandlers);
        Assert.Equal(new SessionRenamed(_sessionId, "Friday"), Assert.Single(_dispatcher.Events));
    }

    [Fact]
    public async Task Rename_OfAnUnknownSession_ChangesNothingAndRaisesNothing()
    {
        var renamed = await _repository.RenameAsync(Guid.NewGuid(), "Friday", Ct);

        Assert.False(renamed);
        Assert.Empty(_dispatcher.Events);
    }

    [Fact]
    public async Task UpdatePasswordHash_SavesItAndDispatchesPasswordChanged()
    {
        var updated = await _repository.UpdatePasswordHashAsync(_sessionId, "new-hash", Ct);

        Assert.True(updated);
        Assert.Equal("new-hash", (await ReadAsync()).PasswordHash);
        Assert.Equal(new SessionPasswordChanged(_sessionId), Assert.Single(_dispatcher.Events));
    }

    [Fact]
    public async Task SetFallbackPlaylist_ThenClear_DispatchesChangedThenCleared()
    {
        await _repository.SetFallbackPlaylistAsync(_sessionId, "playlist", "Mix", true, Ct);
        await _repository.SetFallbackPlaylistAsync(_sessionId, null, null, false, Ct);

        Assert.Collection(
            _dispatcher.Events,
            first => Assert.Equal(new FallbackPlaylistChanged(_sessionId, null, "playlist"), first),
            second => Assert.Equal(new FallbackPlaylistCleared(_sessionId, "playlist"), second));
        Assert.Null((await ReadAsync()).FallbackPlaylistId);
    }

    [Fact]
    public async Task ClearingWhenThereIsNoPlaylist_RaisesNothing()
    {
        var cleared = await _repository.SetFallbackPlaylistAsync(_sessionId, null, null, false, Ct);

        Assert.True(cleared);
        Assert.Empty(_dispatcher.Events);
    }

    [Fact]
    public async Task SetFallbackCursor_SavesItAndRaisesNothing()
    {
        await _repository.SetFallbackPlaylistAsync(_sessionId, "playlist", "Mix", false, Ct);
        _dispatcher.Events.Clear();

        await _repository.SetFallbackCursorAsync(_sessionId, 3, Ct);

        Assert.Equal(3, (await ReadAsync()).FallbackCursor);
        Assert.Empty(_dispatcher.Events);
    }

    [Fact]
    public async Task Claim_DispatchesHostClaimedOnce_AndTheHostLoggingInAgainRaisesNothing()
    {
        Assert.Equal(SessionClaimResult.Claimed, await _repository.TryClaimAsync(_sessionId, "host-1", Ct));
        Assert.Equal(SessionClaimResult.Claimed, await _repository.TryClaimAsync(_sessionId, "host-1", Ct));

        Assert.Equal(new HostClaimed(_sessionId, "host-1"), Assert.Single(_dispatcher.Events));
    }

    [Fact]
    public async Task Claim_ByAnotherAccount_IsRefusedAndRaisesNothing()
    {
        await _repository.TryClaimAsync(_sessionId, "host-1", Ct);
        _dispatcher.Events.Clear();

        var result = await _repository.TryClaimAsync(_sessionId, "host-2", Ct);

        Assert.Equal(SessionClaimResult.AlreadyClaimed, result);
        Assert.Empty(_dispatcher.Events);
        Assert.Equal("host-1", (await ReadAsync()).HostProfileId);
    }

    [Fact]
    public async Task Release_ClearsTheHostAndTheirPlaylist_AndNamesBothInTheEvents()
    {
        await _repository.TryClaimAsync(_sessionId, "host-1", Ct);
        await _repository.SetFallbackPlaylistAsync(_sessionId, "playlist", "Mix", true, Ct);
        _dispatcher.Events.Clear();

        var released = await _repository.ReleaseAsync(_sessionId, Ct);

        Assert.True(released);
        var session = await ReadAsync();
        Assert.Null(session.HostProfileId);
        Assert.Null(session.FallbackPlaylistId);
        Assert.False(session.FallbackShuffle);
        Assert.Collection(
            _dispatcher.Events,
            first => Assert.Equal(new HostReleased(_sessionId, "host-1"), first),
            second => Assert.Equal(new FallbackPlaylistCleared(_sessionId, "playlist"), second));
    }

    [Fact]
    public async Task Release_OfASessionWithoutAHost_StillDispatchesHostReleased()
    {
        await _repository.ReleaseAsync(_sessionId, Ct);

        Assert.Equal(new HostReleased(_sessionId, null), Assert.Single(_dispatcher.Events));
    }

    [Fact]
    public async Task Delete_DispatchesSessionDeletedWithWhatTheCleanupNeeds_AfterTheRowIsGone()
    {
        await _repository.TryClaimAsync(_sessionId, "host-1", Ct);
        await _repository.SetFallbackPlaylistAsync(_sessionId, "playlist", "Mix", false, Ct);
        _dispatcher.Events.Clear();
        Session? seenByHandlers = new("X", "X", "X");
        _dispatcher.OnDispatch = async _ => seenByHandlers = await _repository.GetAsync(_sessionId, Ct);

        var deleted = await _repository.DeleteAsync(_sessionId, Ct);

        Assert.True(deleted);
        Assert.Null(seenByHandlers);
        Assert.Equal(new SessionDeleted(_sessionId, "host-1", "playlist"), Assert.Single(_dispatcher.Events));
    }

    [Fact]
    public async Task Delete_OfAnUnknownSession_RaisesNothing()
    {
        var deleted = await _repository.DeleteAsync(Guid.NewGuid(), Ct);

        Assert.False(deleted);
        Assert.Empty(_dispatcher.Events);
    }
}
