using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Games.Base;
using PartyKing.Domain.Entities.Games.Ringo;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Events;
using PartyKing.Domain.Events.Games.Base;
using PartyKing.Domain.Events.Games.Ringo;
using PartyKing.Infrastructure.Metrics;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>
/// What the lobby repositories dispatch, against a real SQLite file: the events of every saved write, once the change is
/// committed and the session's write lock is free, and nothing for a write that changed nothing or failed.
/// </summary>
public class LobbyGamesRepositoryEventsTests : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly RepositoryMetrics _metrics = new();
    private readonly RecordingDispatcher _dispatcher = new();
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
            _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>(), _writer, _metrics, _dispatcher);
    }

    public async Task DisposeAsync()
    {
        _metrics.Dispose();
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private async Task CreateGameAsync(bool open = false)
    {
        Assert.True(await _repository.TryCreateAsync(RingoGame.Start(_sessionId, 1, DateTimeOffset.UtcNow), Ct));
        if (open)
        {
            await _repository.OpenAsync(_sessionId, Ct);
        }

        _dispatcher.Events.Clear();
    }

    private static RingoPlayer Player(string name) => RingoPlayer.Create(Guid.NewGuid(), name, null, DateTimeOffset.UtcNow);

    [Fact]
    public async Task CreatingAGame_DispatchesStarted_AndASecondOneDispatchesNothing()
    {
        await _repository.TryCreateAsync(RingoGame.Start(_sessionId, 1, DateTimeOffset.UtcNow), Ct);
        Assert.Equal(new RingoStarted(_sessionId), Assert.Single(_dispatcher.Events));
        _dispatcher.Events.Clear();

        var created = await _repository.TryCreateAsync(RingoGame.Start(_sessionId, 2, DateTimeOffset.UtcNow), Ct);

        Assert.False(created);
        Assert.Empty(_dispatcher.Events);
    }

    [Fact]
    public async Task Opening_DispatchesOpenedOnce()
    {
        await CreateGameAsync();

        await _repository.OpenAsync(_sessionId, Ct);
        await _repository.OpenAsync(_sessionId, Ct);

        Assert.Equal(new LobbyOpened(_sessionId, GameKind.Ringo), Assert.Single(_dispatcher.Events));
    }

    [Fact]
    public async Task Joining_DispatchesJoined_AndARefusedJoinDispatchesNothing()
    {
        await CreateGameAsync(open: true);
        var ola = Player("Ola");

        await _repository.JoinAsync(_sessionId, ola, Ct);
        var refused = await _repository.JoinAsync(_sessionId, Player("OLA"), Ct);

        Assert.Equal(LobbyStatus.NameTaken, refused.Status);
        Assert.Equal(new LobbyPlayerJoined(_sessionId, GameKind.Ringo, ola.PlayerId), Assert.Single(_dispatcher.Events));
    }

    [Fact]
    public async Task KickingAndReordering_DispatchTheirEvents()
    {
        await CreateGameAsync(open: true);
        var ola = Player("Ola");
        var ala = Player("Ala");
        await _repository.JoinAsync(_sessionId, ola, Ct);
        await _repository.JoinAsync(_sessionId, ala, Ct);
        _dispatcher.Events.Clear();

        await _repository.ReorderAsync(_sessionId, [ala.PlayerId, ola.PlayerId], Ct);
        await _repository.KickAsync(_sessionId, ola.PlayerId, Ct);
        await _repository.KickAsync(_sessionId, Guid.NewGuid(), Ct);

        IDomainEvent[] expected =
        [
            new LobbyOrderChanged(_sessionId, GameKind.Ringo),
            new LobbyPlayerKicked(_sessionId, GameKind.Ringo, ola.PlayerId),
        ];
        Assert.Equal(expected, _dispatcher.Events);
    }

    [Fact]
    public async Task AChangedUpdate_DispatchesWhatTheActionRaised_AndAnUnchangedOneNothing()
    {
        await CreateGameAsync();

        await _repository.UpdateAsync(
            _sessionId, "FlipAsync", RingoOutcome.NoGame, game => game.Flip(5, "guest", null, DateTimeOffset.UtcNow), Ct);
        await _repository.UpdateAsync(
            _sessionId, "FlipAsync", RingoOutcome.NoGame, game => game.Flip(5, "guest", null, DateTimeOffset.UtcNow), Ct);

        Assert.Equal(new RingoCardFlipped(_sessionId, 5, "guest"), Assert.Single(_dispatcher.Events));
    }

    [Fact]
    public async Task Deleting_DispatchesEnded_AndDeletingNothingDispatchesNothing()
    {
        await CreateGameAsync();

        Assert.True(await _repository.DeleteAsync(_sessionId, Ct));
        Assert.False(await _repository.DeleteAsync(_sessionId, Ct));

        Assert.Equal(new LobbyGameEnded(_sessionId, GameKind.Ringo), Assert.Single(_dispatcher.Events));
    }

    [Fact]
    public async Task TheEvents_AreDispatchedAfterTheChangeIsCommitted()
    {
        await CreateGameAsync();
        bool? openInTheDatabase = null;
        _dispatcher.OnDispatch = async _ =>
        {
            await using var dbContext = await _writer.CreateDbContextAsync();
            openInTheDatabase = (await dbContext.RingoGames.SingleAsync(x => x.SessionId == _sessionId)).IsOpen;
        };

        await _repository.OpenAsync(_sessionId, Ct);

        Assert.True(openInTheDatabase);
    }

    [Fact]
    public async Task AHandler_MayWriteToTheSameSession_BecauseTheLockIsFreeByThen()
    {
        await CreateGameAsync();
        var reentered = false;
        _dispatcher.OnDispatch = async _ =>
        {
            if (reentered)
            {
                return;
            }

            reentered = true;
            await _repository.UpdateAsync(
                _sessionId, "FlipAsync", RingoOutcome.NoGame, game => game.Flip(9, "handler", null, DateTimeOffset.UtcNow), Ct);
        };

        await _repository.OpenAsync(_sessionId, Ct).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Contains(_dispatcher.Events, x => x is RingoCardFlipped { CardId: 9 });
    }
}
