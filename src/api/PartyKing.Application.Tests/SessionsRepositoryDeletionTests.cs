using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Events.Sessions;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Application.Tests;

/// <summary>
/// Scheduling, cancelling and listing the deletion of a session against a real SQLite file: the time is stored, the
/// first request wins, and the due list follows the rule of <c>Session.IsDueForDeletion</c>.
/// </summary>
public class SessionsRepositoryDeletionTests : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Delay = TimeSpan.FromDays(7);

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
        }

        _repository = new SessionsRepository(
            _provider.GetRequiredService<IDbContextFactory<ReaderDbContext>>(), writer, _dispatcher);
        _sessionId = await AddSessionAsync("ABC123");
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private async Task<Guid> AddSessionAsync(string code)
    {
        var session = new Session(Guid.NewGuid(), code, "Party", "hash", Now);
        Assert.True(await _repository.TryAddAsync(session, Ct));

        return session.Id;
    }

    private async Task<DateTime?> ScheduledAtAsync(Guid id)
    {
        return (await _repository.GetAsync(id, Ct))!.DeletionScheduledAt;
    }

    [Fact]
    public async Task ScheduleDeletion_StoresNowPlusTheDelay_AndRaisesTheEventOnce()
    {
        var result = await _repository.ScheduleDeletionAsync(_sessionId, Now, Delay, Ct);

        Assert.Equal(SessionDeletionScheduleResult.Scheduled, result);
        Assert.Equal(Now.Add(Delay), await ScheduledAtAsync(_sessionId));
        Assert.Equal(new SessionDeletionScheduled(_sessionId, Now.Add(Delay)), Assert.Single(_dispatcher.Events));
    }

    [Fact]
    public async Task ScheduleDeletion_Again_KeepsTheFirstTime_AndRaisesNothingMore()
    {
        await _repository.ScheduleDeletionAsync(_sessionId, Now, Delay, Ct);

        var result = await _repository.ScheduleDeletionAsync(_sessionId, Now.AddDays(3), Delay, Ct);

        Assert.Equal(SessionDeletionScheduleResult.AlreadyScheduled, result);
        Assert.Equal(Now.Add(Delay), await ScheduledAtAsync(_sessionId));
        Assert.Single(_dispatcher.Events);
    }

    [Fact]
    public async Task ScheduleDeletion_OfAMissingSession_AnswersNotFound()
    {
        var result = await _repository.ScheduleDeletionAsync(Guid.NewGuid(), Now, Delay, Ct);

        Assert.Equal(SessionDeletionScheduleResult.SessionNotFound, result);
        Assert.Empty(_dispatcher.Events);
    }

    [Fact]
    public async Task CancelDeletion_ClearsTheTime_AndRaisesTheEvent()
    {
        await _repository.ScheduleDeletionAsync(_sessionId, Now, Delay, Ct);
        _dispatcher.Events.Clear();

        var result = await _repository.CancelDeletionAsync(_sessionId, Ct);

        Assert.Equal(SessionDeletionCancelResult.Cancelled, result);
        Assert.Null(await ScheduledAtAsync(_sessionId));
        Assert.Equal(new SessionDeletionCancelled(_sessionId), Assert.Single(_dispatcher.Events));
    }

    [Fact]
    public async Task CancelDeletion_WithoutAScheduledDeletion_ChangesAndRaisesNothing()
    {
        var result = await _repository.CancelDeletionAsync(_sessionId, Ct);

        Assert.Equal(SessionDeletionCancelResult.NotScheduled, result);
        Assert.Empty(_dispatcher.Events);
    }

    [Fact]
    public async Task CancelDeletion_OfAMissingSession_AnswersNotFound()
    {
        Assert.Equal(
            SessionDeletionCancelResult.SessionNotFound,
            await _repository.CancelDeletionAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task ListDueForDeletion_ReturnsTheDueSessionsOldestFirst_AndNotTheOthers()
    {
        var later = await AddSessionAsync("LATER1");
        var earlier = await AddSessionAsync("EARLY1");
        var notYet = await AddSessionAsync("NOTYET");
        // _sessionId stays unscheduled
        await _repository.ScheduleDeletionAsync(later, Now.AddDays(-8), Delay, Ct);
        await _repository.ScheduleDeletionAsync(earlier, Now.AddDays(-9), Delay, Ct);
        await _repository.ScheduleDeletionAsync(notYet, Now.AddDays(-1), Delay, Ct);

        var due = await _repository.ListDueForDeletionAsync(Now, Ct);

        Assert.Equal([earlier, later], due);
    }

    [Fact]
    public async Task ListDueForDeletion_IncludesASessionAtTheExactInstant_AndNotOneTickBefore()
    {
        await _repository.ScheduleDeletionAsync(_sessionId, Now, Delay, Ct);
        var instant = Now.Add(Delay);

        Assert.Empty(await _repository.ListDueForDeletionAsync(instant.AddTicks(-1), Ct));
        Assert.Equal([_sessionId], await _repository.ListDueForDeletionAsync(instant, Ct));
    }

    [Fact]
    public async Task ListDueForDeletion_DoesNotListACancelledDeletion()
    {
        await _repository.ScheduleDeletionAsync(_sessionId, Now, Delay, Ct);
        await _repository.CancelDeletionAsync(_sessionId, Ct);

        Assert.Empty(await _repository.ListDueForDeletionAsync(Now.AddYears(1), Ct));
    }
}
