using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>The OAuth states repository against a real SQLite file: storing, forgetting expired ones and single use.</summary>
public class OAuthStatesRepositoryTests : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"partyking-tests-{Guid.NewGuid():N}");
    private string ConnectionString => $"Data Source={Path.Combine(_directory, "PartyKing.db")}";
    private ServiceProvider _provider = null!;
    private OAuthStatesRepository _repository = null!;
    private Guid _sessionId;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        _provider = new ServiceCollection()
            .AddDbContextFactory<WriterDbContext>(options => options.UseSqlite(ConnectionString))
            .BuildServiceProvider();

        var writer = _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>();
        await using (var dbContext = await writer.CreateDbContextAsync())
        {
            await dbContext.Database.MigrateAsync();

            // The state has a foreign key to its session
            var session = new Session(Guid.NewGuid(), "ABC123", "Party", "hash", DateTime.UtcNow);
            _sessionId = session.Id;
            dbContext.Sessions.Add(session);
            await dbContext.SaveChangesAsync();
        }

        _repository = new OAuthStatesRepository(writer);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private async Task<int> RowCountAsync()
    {
        var writer = _provider.GetRequiredService<IDbContextFactory<WriterDbContext>>();
        await using var dbContext = await writer.CreateDbContextAsync();

        return await dbContext.OAuthStates.CountAsync();
    }

    [Fact]
    public async Task ASavedState_CanBeTakenOnce_WithItsSessionAndExpiration()
    {
        var state = OAuthState.Issue(_sessionId, Now);
        await _repository.AddAsync(state, Now, Ct);

        var taken = await _repository.TakeAsync(state.Id, Ct);
        var again = await _repository.TakeAsync(state.Id, Ct);

        Assert.Equal(_sessionId, taken!.SessionId);
        Assert.Equal(state.ExpiresAt, taken.ExpiresAt);
        Assert.Null(again);
        Assert.Equal(0, await RowCountAsync());
    }

    [Fact]
    public async Task AnUnknownState_IsNull()
    {
        Assert.Null(await _repository.TakeAsync("nope", Ct));
    }

    [Fact]
    public async Task AnExpiredState_IsStillReturnedAndDeleted_ForTheCallerToRefuse()
    {
        var state = OAuthState.Issue(_sessionId, Now);
        await _repository.AddAsync(state, Now, Ct);

        var taken = await _repository.TakeAsync(state.Id, Ct);

        Assert.True(taken!.IsExpired(Now.AddMinutes(11)));
        Assert.Equal(0, await RowCountAsync());
    }

    [Fact]
    public async Task Adding_ForgetsTheStatesThatExpiredBefore_AndKeepsTheOthers()
    {
        var old = OAuthState.Issue(_sessionId, Now);
        var fresh = OAuthState.Issue(_sessionId, Now.AddMinutes(9));
        await _repository.AddAsync(old, Now, Ct);
        await _repository.AddAsync(fresh, Now.AddMinutes(9), Ct);

        var added = OAuthState.Issue(_sessionId, Now.AddMinutes(11));
        await _repository.AddAsync(added, Now.AddMinutes(11), Ct);

        Assert.Null(await _repository.TakeAsync(old.Id, Ct));
        Assert.NotNull(await _repository.TakeAsync(fresh.Id, Ct));
        Assert.NotNull(await _repository.TakeAsync(added.Id, Ct));
    }

    [Fact]
    public async Task TakingTheSameStateAtOnce_LetsExactlyOneCallerWin()
    {
        var state = OAuthState.Issue(_sessionId, Now);
        await _repository.AddAsync(state, Now, Ct);

        var takes = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => Task.Run(() => _repository.TakeAsync(state.Id, Ct))));

        Assert.Single(takes, taken => taken is not null);
    }
}
