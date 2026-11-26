using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Events;
using PartyKing.Application.Ports;
using PartyKing.Application.Sessions.Handlers;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Events.Sessions;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>
/// The host's Spotify profile (display name, refresh token) goes with the session, through the real repositories and
/// the real dispatcher over a SQLite file: the privacy policy promises it.
/// </summary>
public class HostProfileCleanupTests : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");
    private string ConnectionString => $"Data Source={Path.Combine(_directory, "PartyKing.db")}";
    private ServiceProvider _provider = null!;
    private SessionsRepository _sessions = null!;
    private Guid _hosted;
    private Guid _hostless;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        _provider = new ServiceCollection()
            .AddDbContextFactory<WriterDbContext>(options => options.UseSqlite(ConnectionString))
            .AddDbContextFactory<ReaderDbContext>(options => options.UseSqlite(ConnectionString))
            .BuildServiceProvider();

        var reader = _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>();
        var writer = _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>();

        // The dispatcher runs the handler over the real profiles repository, as the API wires it up
        var handlers = new ServiceCollection()
            .AddSingleton<ISpotifyProfilesRepository>(new SpotifyProfilesRepository(reader, writer))
            .AddScoped<IDomainEventHandler<SessionDeleted>, HostProfileCleanupHandler>()
            .BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(
            handlers.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DomainEventDispatcher>.Instance);

        await using (var dbContext = await writer.CreateDbContextAsync())
        {
            await dbContext.Database.MigrateAsync();
            dbContext.SpotifyProfiles.AddRange(new SpotifyProfile("host-1", "refresh-1"), new SpotifyProfile("host-2", "refresh-2"));

            var hosted = new Session(Guid.NewGuid(), "AAA111", "Hosted", "hash", DateTime.UtcNow);
            var hostless = new Session(Guid.NewGuid(), "BBB222", "Hostless", "hash", DateTime.UtcNow);
            _hosted = hosted.Id;
            _hostless = hostless.Id;
            dbContext.Sessions.AddRange(hosted, hostless);
            await dbContext.SaveChangesAsync();
        }

        _sessions = new SessionsRepository(reader, writer, dispatcher);
        await _sessions.TryClaimAsync(_hosted, "host-1", Ct);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private async Task<List<string>> ProfileIdsAsync()
    {
        await using var dbContext = await _provider
            .GetRequiredService<IDbContextFactory<ReaderDbContext>>()
            .CreateDbContextAsync();

        return await dbContext.SpotifyProfiles.Select(x => x.Id).OrderBy(x => x).ToListAsync();
    }

    [Fact]
    public async Task DeletingASession_DeletesItsHostsProfile_AndNoOtherProfile()
    {
        await _sessions.DeleteAsync(_hosted, Ct);

        Assert.Equal(["host-2"], await ProfileIdsAsync());
    }

    [Fact]
    public async Task DeletingASessionWithoutAHost_DeletesNoProfile()
    {
        await _sessions.DeleteAsync(_hostless, Ct);

        Assert.Equal(["host-1", "host-2"], await ProfileIdsAsync());
    }
}
