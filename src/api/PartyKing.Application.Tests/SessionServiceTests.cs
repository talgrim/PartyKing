using ErrorOr;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Demo.Services;
using PartyKing.Application.System;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Sessions;
using PartyKing.Contract.V1.Sessions;

namespace PartyKing.Application.Tests;

public class SessionServiceTests
{
    private const string HostId = "host-1";

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeClock _clock = new();
    private readonly SessionService _service;
    private readonly Guid _id;

    private static DemoMode Demo(DemoConfiguration configuration) => new(Options.Create(configuration), new FakeClock());

    private SessionService CreateService(DemoConfiguration demo, SessionDeletionConfiguration? deletion = null)
    {
        return new SessionService(
            _sessions,
            new SessionAuthorizer(_sessions),
            Demo(demo),
            _clock,
            Options.Create(deletion ?? new SessionDeletionConfiguration()));
    }

    public SessionServiceTests()
    {
        _service = CreateService(new DemoConfiguration());
        _id = _sessions.AddSession(HostId, new PasswordHasher().Hash("secret")).Id;
    }

    [Fact]
    public async Task TheList_InTheDemo_IsEmpty_SoVisitorsDoNotSeeEachOthersSessions()
    {
        var service = CreateService(new DemoConfiguration { Enabled = true });

        var sessions = await service.GetAllAsync(CancellationToken.None);

        Assert.Empty(sessions);
    }

    [Fact]
    public async Task TheList_SaysWhetherASessionHasAHost_ButNotWho()
    {
        var withoutHost = _sessions.AddSession(null, new PasswordHasher().Hash("secret"));

        var sessions = await _service.GetAllAsync(CancellationToken.None);

        Assert.Equal(2, sessions.Count);
        Assert.True(sessions.Single(x => x.Id == _id).HasHost);
        Assert.False(sessions.Single(x => x.Id == withoutHost.Id).HasHost);
        Assert.DoesNotContain("HostName", typeof(SessionListItemDto).GetProperties().Select(x => x.Name));
    }

    [Fact]
    public async Task TheHost_CanRenameTheSession()
    {
        var result = await _service.RenameAsync(_id, "New name", new SessionCaller(HostId, false), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("New name", _sessions.Renamed.Single().Name);
    }

    [Fact]
    public async Task Renaming_TrimsTheNameBeforeCheckingItsLength()
    {
        var result = await _service.RenameAsync(_id, $"  {new string('a', 60)}  ", new SessionCaller(HostId, false), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(new string('a', 60), _sessions.Renamed.Single().Name);
    }

    [Fact]
    public async Task Renaming_ToAnOverlongName_IsRejectedAndChangesNothing()
    {
        var result = await _service.RenameAsync(_id, new string('a', 61), new SessionCaller(HostId, false), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Empty(_sessions.Renamed);
    }

    [Fact]
    public async Task RenamingTheSession_AsAGuest_IsForbiddenAndChangesNothing()
    {
        var result = await _service.RenameAsync(_id, "New name", new SessionCaller("someone-else", false), CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Empty(_sessions.Renamed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task RenamingTheSession_ToABlankName_IsRejected(string? name)
    {
        var result = await _service.RenameAsync(_id, name, new SessionCaller(HostId, false), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task TheHost_CanDeleteTheSessionAtOnce()
    {
        var result = await _service.DeleteAsync(_id, new SessionCaller(HostId, false), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Null(await _sessions.GetAsync(_id, CancellationToken.None));
    }

    [Fact]
    public async Task AnAdmin_CanDeleteASessionTheyDoNotHost()
    {
        var result = await _service.DeleteAsync(_id, new SessionCaller(null, true), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Null(await _sessions.GetAsync(_id, CancellationToken.None));
    }

    [Fact]
    public async Task DeletingTheSession_AsAGuest_IsForbiddenAndDeletesNothing()
    {
        var result = await _service.DeleteAsync(_id, new SessionCaller("someone-else", false), CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.NotNull(await _sessions.GetAsync(_id, CancellationToken.None));
    }

    [Fact]
    public async Task DeletingAMissingSession_IsNotFound()
    {
        var result = await _service.DeleteAsync(Guid.NewGuid(), new SessionCaller(null, true), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task DeletingTheSession_InTheDemo_IsRefused_BecauseItsHostProfileWouldBeLeftBehind()
    {
        var service = CreateService(new DemoConfiguration { Enabled = true });

        var result = await service.DeleteAsync(_id, new SessionCaller(HostId, false), CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.NotNull(await _sessions.GetAsync(_id, CancellationToken.None));
    }

    [Fact]
    public async Task RequestingDeletion_SchedulesItForTheConfiguredDelayFromNow()
    {
        var service = CreateService(new DemoConfiguration(), new SessionDeletionConfiguration { DelayDays = 7 });

        var result = await service.RequestDeletionAsync(_id, CancellationToken.None);

        Assert.False(result.IsError);
        var session = await _sessions.GetAsync(_id, CancellationToken.None);
        Assert.Equal(_clock.UtcNow.UtcDateTime.AddDays(7), session!.DeletionScheduledAt);
    }

    [Fact]
    public async Task RequestingDeletion_Again_SucceedsAndKeepsTheFirstTime()
    {
        await _service.RequestDeletionAsync(_id, CancellationToken.None);
        var first = (await _sessions.GetAsync(_id, CancellationToken.None))!.DeletionScheduledAt;
        _clock.Advance(TimeSpan.FromDays(3));

        var result = await _service.RequestDeletionAsync(_id, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(first, (await _sessions.GetAsync(_id, CancellationToken.None))!.DeletionScheduledAt);
    }

    [Fact]
    public async Task RequestingDeletion_OfAMissingSession_IsNotFound()
    {
        var result = await _service.RequestDeletionAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task RequestingDeletion_InTheDemo_IsRefused_BecauseTheDemoHasItsOwnExpiry()
    {
        var service = CreateService(new DemoConfiguration { Enabled = true });

        var result = await service.RequestDeletionAsync(_id, CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Null((await _sessions.GetAsync(_id, CancellationToken.None))!.DeletionScheduledAt);
    }
}
