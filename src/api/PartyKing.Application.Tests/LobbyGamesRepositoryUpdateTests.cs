using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Games;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Infrastructure.Metrics;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>
/// The generic <c>UpdateAsync</c> of the lobby repositories, run on the Ringo one against a real SQLite file: the
/// action runs on the loaded game, the result decides whether it is saved, and a save drops the shared reads.
/// </summary>
public class LobbyGamesRepositoryUpdateTests : IAsyncLifetime
{
    private sealed record Outcome(bool Changed, int? SeenCardId = null) : IGameUpdateResult;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");
    private string ConnectionString => $"Data Source={Path.Combine(_directory, "PartyKing.db")}";
    private ServiceProvider _provider = null!;
    private IDbContextFactory<WriterDbContext> _writer = null!;
    private RingoGamesRepository _repository = null!;
    private Guid _sessionId;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        _provider = new ServiceCollection()
            .AddDbContextFactory<WriterDbContext>(options => options.UseSqlite(ConnectionString))
            .AddDbContextFactory<ReaderDbContext>(options => options.UseSqlite(ConnectionString))
            .BuildServiceProvider();

        _writer = _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>();
        await using (var dbContext = await _writer.CreateDbContextAsync())
        {
            await dbContext.Database.MigrateAsync();

            var session = new Session("ABC123", "Party", "hash");
            _sessionId = session.Id;
            dbContext.Sessions.Add(session);
            await dbContext.SaveChangesAsync();
        }

        _repository = new RingoGamesRepository(
            _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>(),
            _writer);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private async Task CreateGameAsync()
    {
        Assert.True(await _repository.TryCreateAsync(
            new RingoGame { SessionId = _sessionId, Seed = 1, CreatedAt = DateTimeOffset.UtcNow },
            CancellationToken.None));
    }

    private async Task<int?> StoredPendingCardAsync()
    {
        await using var dbContext = await _writer.CreateDbContextAsync();

        return (await dbContext.RingoGames.SingleAsync(x => x.SessionId == _sessionId)).PendingCardId;
    }

    [Fact]
    public async Task WithoutAGame_ReturnsTheNoGameResultAndNeverRunsTheAction()
    {
        var ran = false;
        var noGame = new Outcome(false);

        var result = await _repository.UpdateAsync(
            _sessionId,
            "TestAsync",
            noGame,
            _ =>
            {
                ran = true;
                return new Outcome(true);
            },
            CancellationToken.None);

        Assert.Same(noGame, result);
        Assert.False(ran);
    }

    [Fact]
    public async Task AChangedResult_SavesWhatTheActionDid()
    {
        await CreateGameAsync();

        var result = await _repository.UpdateAsync(
            _sessionId,
            "TestAsync",
            new Outcome(false),
            game =>
            {
                game.PendingCardId = 7;
                return new Outcome(true);
            },
            CancellationToken.None);

        Assert.True(result.Changed);
        Assert.Equal(7, await StoredPendingCardAsync());
    }

    [Fact]
    public async Task AnUnchangedResult_SavesNothing()
    {
        await CreateGameAsync();

        var result = await _repository.UpdateAsync(
            _sessionId,
            "TestAsync",
            new Outcome(false),
            game =>
            {
                game.PendingCardId = 7;
                return new Outcome(false);
            },
            CancellationToken.None);

        Assert.False(result.Changed);
        Assert.Null(await StoredPendingCardAsync());
    }

    [Fact]
    public async Task TheActionSeesTheLoadedGameWithItsRelations()
    {
        await CreateGameAsync();
        await _repository.UpdateAsync(
            _sessionId,
            "SeedAsync",
            new Outcome(false),
            game =>
            {
                game.Flips.Add(new RingoFlip { SessionId = _sessionId, Order = 0, CardId = 3, FlippedAt = DateTimeOffset.UtcNow, FlippedBy = "admin" });
                game.PendingCardId = 3;
                return new Outcome(true);
            },
            CancellationToken.None);

        var result = await _repository.UpdateAsync(
            _sessionId,
            "ReadAsync",
            new Outcome(false),
            game => new Outcome(false, game.Flips.Single().CardId),
            CancellationToken.None);

        Assert.Equal(3, result.SeenCardId);
    }

    [Fact]
    public async Task ASavedUpdate_IsVisibleToTheNextRead()
    {
        await CreateGameAsync();
        Assert.Null((await _repository.GetAsync(_sessionId, CancellationToken.None))!.PendingCardId);

        await _repository.UpdateAsync(
            _sessionId,
            "TestAsync",
            new Outcome(false),
            game =>
            {
                game.PendingCardId = 9;
                return new Outcome(true);
            },
            CancellationToken.None);

        Assert.Equal(9, (await _repository.GetAsync(_sessionId, CancellationToken.None))!.PendingCardId);
    }

    [Fact]
    public async Task TheMeteredDecorator_RecordsTheOperationNameItWasGiven()
    {
        await CreateGameAsync();
        var tagSets = new List<Dictionary<string, object?>>();
        using var metrics = new RepositoryMetrics();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == RepositoryMetrics.DurationInstrumentName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
            tagSets.Add(tags.ToArray().ToDictionary(t => t.Key, t => t.Value)));
        listener.Start();
        var metered = new MeteredRingoGamesRepository(_repository, metrics);

        await metered.UpdateAsync(
            _sessionId, "FlipAsync", new Outcome(false), _ => new Outcome(false), CancellationToken.None);

        var tags = Assert.Single(tagSets);
        Assert.Equal("RingoGamesRepository", tags["repository"]);
        Assert.Equal("FlipAsync", tags["operation"]);
        Assert.Equal(RepositoryMetrics.SuccessOutcome, tags["outcome"]);
    }
}
