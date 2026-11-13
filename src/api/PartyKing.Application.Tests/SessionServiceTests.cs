using ErrorOr;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Demo.Services;
using PartyKing.Application.Accounts.Services;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Sessions;
using PartyKing.Contract.V1.Sessions;

namespace PartyKing.Application.Tests;

public class SessionServiceTests
{
    private const string HostId = "host-1";

    private readonly FakeSessionsRepository _sessions = new();
    private readonly SessionService _service;
    private readonly Guid _id;

    private static DemoMode Demo(DemoConfiguration configuration) => new(Options.Create(configuration), new FakeClock());

    public SessionServiceTests()
    {
        _service = new SessionService(_sessions, new SessionAuthorizer(_sessions), Demo(new DemoConfiguration()));
        _id = _sessions.AddSession(HostId, new PasswordHasher().Hash("secret")).Id;
    }

    [Fact]
    public async Task TheList_InTheDemo_IsEmpty_SoVisitorsDoNotSeeEachOthersSessions()
    {
        var service = new SessionService(
            _sessions,
            new SessionAuthorizer(_sessions),
            Demo(new DemoConfiguration { Enabled = true }));

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
}
