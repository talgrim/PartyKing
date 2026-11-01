using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Games;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Results;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>
/// Runs the Ringo flip and continue rules against a real SQLite file (see <see cref="RingoSessionDeletionTests"/>),
/// so the guard order, the turn handling and what is persisted are pinned independently of the service fake.
/// </summary>
public class RingoGamesRepositoryTests : IAsyncLifetime
{
    // Card ids run by suit, 13 ranks each, 2 to ace: the king is the rank at index 11
    private static readonly int[] Kings = [11, 24, 37, 50];

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
        await _repository.OpenAsync(_sessionId, CancellationToken.None);
    }

    private async Task<List<Guid>> CreateGameWithPlayersAsync(params string[] names)
    {
        await CreateGameAsync();

        var ids = new List<Guid>();
        foreach (var name in names)
        {
            var player = new RingoPlayer
            {
                SessionId = _sessionId,
                PlayerId = Guid.NewGuid(),
                Name = name,
                NormalizedName = name.ToUpperInvariant(),
                JoinedAt = DateTimeOffset.UtcNow,
            };
            var joined = await _repository.JoinAsync(_sessionId, player, CancellationToken.None);
            Assert.Equal(LobbyStatus.Done, joined.Status);
            ids.Add(player.PlayerId);
        }

        return ids;
    }

    private Task<RingoFlipResult> FlipAsync(int cardId, Guid? turnOf = null, string flippedBy = "admin")
    {
        return _repository.FlipAsync(_sessionId, cardId, flippedBy, turnOf, DateTimeOffset.UtcNow, CancellationToken.None);
    }

    private Task<RingoContinueResult> ContinueAsync(Guid? turnOf = null)
    {
        return _repository.ContinueAsync(_sessionId, turnOf, CancellationToken.None);
    }

    private async Task<RingoGame> ReloadAsync()
    {
        await using var dbContext = await _writer.CreateDbContextAsync();

        return await dbContext.RingoGames
            .Include(x => x.Flips)
            .Include(x => x.Players)
            .SingleAsync(x => x.SessionId == _sessionId);
    }

    [Fact]
    public async Task Flip_WithoutAGame_ReportsNoGame()
    {
        var flip = await FlipAsync(3);
        var next = await ContinueAsync();

        Assert.Equal(RingoFlipStatus.NoGame, flip.Status);
        Assert.Null(flip.Game);
        Assert.Equal(RingoContinueStatus.NoGame, next.Status);
        Assert.Null(next.Game);
    }

    [Fact]
    public async Task Flip_RecordsTheCardAndMakesItPending()
    {
        await CreateGameAsync();

        var flip = await FlipAsync(5, flippedBy: "guest");

        Assert.Equal(RingoFlipStatus.Flipped, flip.Status);
        var game = await ReloadAsync();
        Assert.Equal(5, game.PendingCardId);
        var recorded = Assert.Single(game.Flips);
        Assert.Equal(0, recorded.Order);
        Assert.Equal(5, recorded.CardId);
        Assert.Equal("guest", recorded.FlippedBy);
    }

    [Fact]
    public async Task Flip_WhileACardIsPending_ChangesNothing()
    {
        await CreateGameAsync();
        await FlipAsync(5);

        var flip = await FlipAsync(6);

        Assert.Equal(RingoFlipStatus.CardPending, flip.Status);
        var game = await ReloadAsync();
        Assert.Equal(5, game.PendingCardId);
        Assert.Single(game.Flips);
    }

    [Fact]
    public async Task Flip_OfAFlippedCard_ReportsAlreadyFlippedBeforeThePendingCheck()
    {
        await CreateGameAsync();
        await FlipAsync(5);

        var flip = await FlipAsync(5);

        Assert.Equal(RingoFlipStatus.AlreadyFlipped, flip.Status);
        Assert.Single((await ReloadAsync()).Flips);
    }

    [Fact]
    public async Task Flip_AfterTheFourthKing_ReportsGameOver()
    {
        await CreateGameAsync();
        foreach (var king in Kings)
        {
            Assert.Equal(RingoFlipStatus.Flipped, (await FlipAsync(king)).Status);
            if (king != Kings[^1])
            {
                await ContinueAsync();
            }
        }

        await ContinueAsync();
        var flip = await FlipAsync(2);

        Assert.Equal(RingoFlipStatus.GameOver, flip.Status);
        var game = await ReloadAsync();
        Assert.True(game.IsOver);
        Assert.Equal(4, game.Flips.Count);
    }

    [Fact]
    public async Task Flip_ChecksTheTurnBeforeTheOtherGuards()
    {
        var players = await CreateGameWithPlayersAsync("Ola", "Bartek");
        await FlipAsync(5, turnOf: players[0]);

        var wrongTurn = await FlipAsync(5, turnOf: players[1]);

        Assert.Equal(RingoFlipStatus.NotYourTurn, wrongTurn.Status);
    }

    [Fact]
    public async Task Flip_ByThePlayerWhoseTurnItIs_UsesTheirNameAndKeepsTheTurn()
    {
        var players = await CreateGameWithPlayersAsync("Ola", "Bartek");

        var flip = await FlipAsync(5, turnOf: players[0], flippedBy: "ignored");

        Assert.Equal(RingoFlipStatus.Flipped, flip.Status);
        var game = await ReloadAsync();
        Assert.Equal("Ola", Assert.Single(game.Flips).FlippedBy);
        Assert.Equal(players[0], game.CurrentPlayerId);
    }

    [Fact]
    public async Task Flip_ByAManagerWithPlayers_IsRecordedAsTheCurrentPlayer()
    {
        var players = await CreateGameWithPlayersAsync("Ola", "Bartek");

        var flip = await FlipAsync(5, turnOf: null, flippedBy: "admin");

        Assert.Equal(RingoFlipStatus.Flipped, flip.Status);
        Assert.Equal("Ola", Assert.Single((await ReloadAsync()).Flips).FlippedBy);
        Assert.Equal(players[0], (await ReloadAsync()).CurrentPlayerId);
    }

    [Fact]
    public async Task Continue_ClearsThePendingCardAndPassesTheTurnOn()
    {
        var players = await CreateGameWithPlayersAsync("Ola", "Bartek", "Cezary");
        await FlipAsync(5, turnOf: players[0]);

        var next = await ContinueAsync(turnOf: players[0]);

        Assert.Equal(RingoContinueStatus.Continued, next.Status);
        var game = await ReloadAsync();
        Assert.Null(game.PendingCardId);
        Assert.Equal(players[1], game.CurrentPlayerId);
    }

    [Fact]
    public async Task Continue_PassesTheTurnRoundToTheFirstPlayer()
    {
        var players = await CreateGameWithPlayersAsync("Ola", "Bartek");
        await FlipAsync(5, turnOf: players[0]);
        await ContinueAsync(turnOf: players[0]);
        await FlipAsync(6, turnOf: players[1]);

        await ContinueAsync(turnOf: players[1]);

        Assert.Equal(players[0], (await ReloadAsync()).CurrentPlayerId);
    }

    [Fact]
    public async Task Continue_ByAnotherPlayer_ReportsNotYourTurn()
    {
        var players = await CreateGameWithPlayersAsync("Ola", "Bartek");
        await FlipAsync(5, turnOf: players[0]);

        var next = await ContinueAsync(turnOf: players[1]);

        Assert.Equal(RingoContinueStatus.NotYourTurn, next.Status);
        Assert.Equal(5, (await ReloadAsync()).PendingCardId);
    }

    [Fact]
    public async Task Continue_WithoutAPendingCard_IsAnIdempotentSuccess()
    {
        var players = await CreateGameWithPlayersAsync("Ola", "Bartek");

        var next = await ContinueAsync(turnOf: players[0]);

        Assert.Equal(RingoContinueStatus.Continued, next.Status);
        Assert.Equal(players[0], (await ReloadAsync()).CurrentPlayerId);
    }

    [Fact]
    public async Task AFlipIsVisibleToTheNextRead()
    {
        await CreateGameAsync();
        var before = await _repository.GetAsync(_sessionId, CancellationToken.None);
        Assert.Empty(before!.Flips);

        await FlipAsync(5);

        var after = await _repository.GetAsync(_sessionId, CancellationToken.None);
        Assert.Equal(5, after!.PendingCardId);
        Assert.Single(after.Flips);
    }

    /// <summary>
    /// There is no concurrency token, so two flips that load the game before either saves both pass the guards, and
    /// the unique index on (session, order) is what stops the second one. Today that loser gets a
    /// <see cref="DbUpdateException"/> instead of <see cref="RingoFlipStatus.CardPending"/>; this test pins what must
    /// hold either way (one flip is stored and it is the pending card) and tolerates both outcomes of the loser.
    /// </summary>
    [Fact]
    public async Task ConcurrentFlips_StoreExactlyOneFlipAndMakeItPending()
    {
        await CreateGameAsync();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(card => Task.Run(async () =>
        {
            try
            {
                return (await FlipAsync(card)).Status;
            }
            catch (DbUpdateException)
            {
                return (RingoFlipStatus?)null;
            }
        })));

        var game = await ReloadAsync();
        Assert.Equal(1, outcomes.Count(x => x == RingoFlipStatus.Flipped));
        Assert.All(
            outcomes.Where(x => x != RingoFlipStatus.Flipped),
            x => Assert.True(x is null or RingoFlipStatus.CardPending));
        Assert.Single(game.Flips);
        Assert.Equal(game.Flips[0].CardId, game.PendingCardId);
    }
}
