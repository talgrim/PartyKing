using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Games;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>
/// Runs against a real SQLite file (see <see cref="QueueRepositoryTests"/>): deleting a session must leave no Ringo
/// row behind, because the cascade delete is configured in the model and no unit test with a fake sees it.
/// </summary>
public class RingoSessionDeletionTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");
    private string ConnectionString => $"Data Source={Path.Combine(_directory, "PartyKing.db")}";
    private ServiceProvider _provider = null!;
    private IDbContextFactory<WriterDbContext> _writer = null!;
    private RingoGamesRepository _ringo = null!;
    private SessionsRepository _sessions = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        _provider = new ServiceCollection()
            .AddDbContextFactory<WriterDbContext>(options => options.UseSqlite(ConnectionString))
            .AddDbContextFactory<ReaderDbContext>(options => options.UseSqlite(ConnectionString))
            .BuildServiceProvider();

        _writer = _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>();
        var reader = _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>();

        await using (var dbContext = await _writer.CreateDbContextAsync())
        {
            await dbContext.Database.MigrateAsync();
        }

        _ringo = new RingoGamesRepository(reader, _writer);
        _sessions = new SessionsRepository(reader, _writer);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private async Task<Guid> CreateSessionWithGameAsync(string code, int seed)
    {
        var session = new Session(code, "Party", "hash");
        Assert.True(await _sessions.TryAddAsync(session, CancellationToken.None));
        Assert.True(await _ringo.TryCreateAsync(
            new RingoGame { SessionId = session.Id, Seed = seed, CreatedAt = DateTimeOffset.UtcNow },
            CancellationToken.None));
        await _ringo.OpenAsync(session.Id, CancellationToken.None);

        var host = await _ringo.JoinAsync(session.Id, NewPlayer(session.Id, "Ola", profileId: "spotify-host"), CancellationToken.None);
        var guest = await _ringo.JoinAsync(session.Id, NewPlayer(session.Id, "Bartek"), CancellationToken.None);
        Assert.Equal(LobbyStatus.Done, host.Status);
        Assert.Equal(LobbyStatus.Done, guest.Status);

        var flip = await _ringo.FlipAsync(session.Id, 5, "admin", turnOf: null, DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.Equal(RingoFlipStatus.Flipped, flip.Status);
        await _ringo.ContinueAsync(session.Id, turnOf: null, CancellationToken.None);
        await _ringo.FlipAsync(session.Id, 12, "admin", turnOf: null, DateTimeOffset.UtcNow, CancellationToken.None);

        return session.Id;
    }

    private static RingoPlayer NewPlayer(Guid sessionId, string name, string? profileId = null) => new()
    {
        SessionId = sessionId,
        PlayerId = Guid.NewGuid(),
        Name = name,
        NormalizedName = (profileId is null ? name : name + LobbyPlayer.HostSuffix).ToUpperInvariant(),
        JoinedAt = DateTimeOffset.UtcNow,
        ProfileId = profileId,
    };

    private async Task<(int Games, int Flips, int Players)> CountAsync(Guid sessionId)
    {
        await using var dbContext = await _writer.CreateDbContextAsync();
        return (
            await dbContext.RingoGames.CountAsync(x => x.SessionId == sessionId),
            await dbContext.RingoFlips.CountAsync(x => x.SessionId == sessionId),
            await dbContext.RingoPlayers.CountAsync(x => x.SessionId == sessionId));
    }

    [Fact]
    public async Task SeedingBuildsAGameWithFlipsAndPlayers()
    {
        var sessionId = await CreateSessionWithGameAsync("AAA111", seed: 1);

        Assert.Equal((1, 2, 2), await CountAsync(sessionId));
    }

    [Fact]
    public async Task DeletingASession_DeletesItsRingoGameFlipsAndPlayers()
    {
        var sessionId = await CreateSessionWithGameAsync("AAA111", seed: 1);

        Assert.True(await _sessions.DeleteAsync(sessionId, CancellationToken.None));

        Assert.Equal((0, 0, 0), await CountAsync(sessionId));
        Assert.Null(await _ringo.GetAsync(sessionId, CancellationToken.None));
    }

    [Fact]
    public async Task DeletingASession_KeepsTheRingoDataOfOtherSessions()
    {
        var deleted = await CreateSessionWithGameAsync("AAA111", seed: 1);
        var kept = await CreateSessionWithGameAsync("BBB222", seed: 2);

        await _sessions.DeleteAsync(deleted, CancellationToken.None);

        Assert.Equal((1, 2, 2), await CountAsync(kept));
        var game = await _ringo.GetAsync(kept, CancellationToken.None);
        Assert.NotNull(game);
        Assert.Equal(["Ola", "Bartek"], game.Players.OrderBy(x => x.Order).Select(x => x.Name));
    }

    [Fact]
    public async Task DeletingASession_LeavesNoRowInAnyTableKeyedBySession()
    {
        // Guards tables added to the model later: any entity with a SessionId must go with its session
        var sessionId = await CreateSessionWithGameAsync("AAA111", seed: 1);

        await using var dbContext = await _writer.CreateDbContextAsync();
        var sessionKeyed = dbContext.Model.GetEntityTypes()
            .Where(x => x.FindProperty("SessionId") is not null)
            .ToList();
        Assert.Contains(sessionKeyed, x => x.ClrType == typeof(RingoGame));
        Assert.Contains(sessionKeyed, x => x.ClrType == typeof(RingoFlip));
        Assert.Contains(sessionKeyed, x => x.ClrType == typeof(RingoPlayer));

        async Task<int> RowsAsync(string table) => await dbContext.Database
            .SqlQueryRaw<int>(
                $"SELECT COUNT(*) AS \"Value\" FROM \"{table}\" WHERE \"SessionId\" = {{0}}",
                sessionId.ToString().ToUpperInvariant())
            .SingleAsync();

        // The query must see the rows first, or "none left" below would prove nothing
        Assert.True(await RowsAsync("RingoGames") > 0);

        await _sessions.DeleteAsync(sessionId, CancellationToken.None);

        foreach (var table in sessionKeyed.Select(x => x.GetTableName()!))
        {
            var remaining = await RowsAsync(table);
            Assert.True(remaining == 0, $"{table} still has {remaining} row(s) of the deleted session");
        }
    }
}
