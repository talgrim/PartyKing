using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Sessions;

namespace PartyKing.Application.Tests;

public class ScheduledSessionDeletionSweepTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly TimeSpan Delay = TimeSpan.FromDays(7);

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeSlideshowService _slideshows = new();
    private readonly FakeClock _clock = new();

    private ScheduledSessionDeletionSweep CreateSweep(ISessionAdminService? admin = null)
    {
        return new ScheduledSessionDeletionSweep(
            _sessions,
            admin ?? SessionTestServices.Admin(_sessions, slideshows: _slideshows),
            _clock,
            NullLogger<ScheduledSessionDeletionSweep>.Instance);
    }

    /// <summary>A session whose deletion was requested now, so it is due after the delay.</summary>
    private async Task<Guid> AddScheduledAsync()
    {
        var session = _sessions.AddSession();
        await _sessions.ScheduleDeletionAsync(session.Id, _clock.UtcNow.UtcDateTime, Delay, Ct);

        return session.Id;
    }

    [Fact]
    public async Task Sweep_DeletesTheSessionsWhoseDeletionIsDue_WithTheirPhotos_AndKeepsTheOthers()
    {
        var due = await AddScheduledAsync();
        _clock.Advance(TimeSpan.FromDays(3));
        var notYet = await AddScheduledAsync();
        var unscheduled = _sessions.AddSession().Id;
        _clock.Advance(TimeSpan.FromDays(4));

        var deleted = await CreateSweep().SweepAsync(Ct);

        Assert.Equal(1, deleted);
        Assert.Null(await _sessions.GetAsync(due, Ct));
        Assert.Equal([due], _slideshows.DeletedSessions);
        Assert.NotNull(await _sessions.GetAsync(notYet, Ct));
        Assert.NotNull(await _sessions.GetAsync(unscheduled, Ct));
    }

    [Fact]
    public async Task Sweep_KeepsASessionUntilTheInstantItsDeletionIsDue()
    {
        var session = await AddScheduledAsync();
        var sweep = CreateSweep();

        _clock.Advance(Delay - TimeSpan.FromSeconds(1));
        var before = await sweep.SweepAsync(Ct);
        _clock.Advance(TimeSpan.FromSeconds(1));
        var at = await sweep.SweepAsync(Ct);

        Assert.Equal(0, before);
        Assert.Equal(1, at);
        Assert.Null(await _sessions.GetAsync(session, Ct));
    }

    [Fact]
    public async Task Sweep_DoesNotDeleteASessionWhoseDeletionWasCancelled()
    {
        var session = await AddScheduledAsync();
        await _sessions.CancelDeletionAsync(session, Ct);
        _clock.Advance(Delay * 2);

        var deleted = await CreateSweep().SweepAsync(Ct);

        Assert.Equal(0, deleted);
        Assert.NotNull(await _sessions.GetAsync(session, Ct));
    }

    [Fact]
    public async Task Sweep_TreatsASessionThatIsAlreadyGone_AsDone_AndStillDeletesTheOthers()
    {
        await AddScheduledAsync();
        var other = await AddScheduledAsync();
        _clock.Advance(Delay);
        // the host deletes the first one between the listing and the delete
        var admin = new DeletingBehindTheSweep(SessionTestServices.Admin(_sessions, slideshows: _slideshows), _sessions);

        var deleted = await CreateSweep(admin).SweepAsync(Ct);

        Assert.Equal(1, deleted);
        Assert.Null(await _sessions.GetAsync(other, Ct));
    }

    [Fact]
    public async Task Sweep_WhenOneSessionCannotBeDeleted_StillDeletesTheOthers()
    {
        var stuck = await AddScheduledAsync();
        var other = await AddScheduledAsync();
        _clock.Advance(Delay);
        var admin = new FailingAdmin(SessionTestServices.Admin(_sessions, slideshows: _slideshows), failFor: stuck);

        var deleted = await CreateSweep(admin).SweepAsync(Ct);

        Assert.Equal(1, deleted);
        Assert.NotNull(await _sessions.GetAsync(stuck, Ct));
        Assert.Null(await _sessions.GetAsync(other, Ct));
    }

    [Fact]
    public async Task Sweep_WithNothingDue_DeletesNothing()
    {
        await AddScheduledAsync();

        var deleted = await CreateSweep().SweepAsync(Ct);

        Assert.Equal(0, deleted);
        Assert.Empty(_slideshows.DeletedSessions);
    }

    /// <summary>Deletes the first session it is asked about before the real delete, like a host who was quicker.</summary>
    private sealed class DeletingBehindTheSweep(ISessionAdminService inner, FakeSessionsRepository sessions)
        : ForwardingAdmin(inner)
    {
        private bool _first = true;

        public override async Task<ErrorOr<Success>> DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            if (_first)
            {
                _first = false;
                await sessions.DeleteAsync(id, cancellationToken);
            }

            return await inner.DeleteAsync(id, cancellationToken);
        }
    }

    private sealed class FailingAdmin(ISessionAdminService inner, Guid failFor) : ForwardingAdmin(inner)
    {
        public override Task<ErrorOr<Success>> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
            id == failFor ? throw new InvalidOperationException("the database is locked") : inner.DeleteAsync(id, cancellationToken);
    }

    private abstract class ForwardingAdmin(ISessionAdminService inner) : ISessionAdminService
    {
        public Task<IReadOnlyList<SessionDto>> GetAllAsync(CancellationToken cancellationToken) =>
            inner.GetAllAsync(cancellationToken);

        public Task<ErrorOr<SessionDto>> CreateAsync(string? name, string? password, CancellationToken cancellationToken) =>
            inner.CreateAsync(name, password, cancellationToken);

        public abstract Task<ErrorOr<Success>> DeleteAsync(Guid id, CancellationToken cancellationToken);

        public Task<ErrorOr<Success>> ReleaseAsync(Guid id, CancellationToken cancellationToken) =>
            inner.ReleaseAsync(id, cancellationToken);

        public Task<ErrorOr<Success>> CancelDeletionAsync(Guid id, CancellationToken cancellationToken) =>
            inner.CancelDeletionAsync(id, cancellationToken);
    }
}
