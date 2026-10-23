using PartyKing.Application.Admin.Services;
using PartyKing.Application.Sessions.Services;

namespace PartyKing.Application.Tests;

public class ViewerRoleTests
{
    private readonly FakeSessionsRepository _sessions = new();
    private readonly SessionService _service;

    public ViewerRoleTests()
    {
        _service = new SessionService(_sessions, new PasswordHasher());
    }

    [Fact]
    public async Task TheLoggedInHost_IsHost()
    {
        var session = _sessions.AddSession("host-profile");

        var role = (await _service.GetViewerRoleAsync(session.Id, "host-profile", false, CancellationToken.None)).Value;

        Assert.True(role.IsHost);
        Assert.False(role.IsAdmin);
    }

    [Fact]
    public async Task TheHostOfAnotherSession_IsNotHostHere()
    {
        var session = _sessions.AddSession("host-profile");

        var role = (await _service.GetViewerRoleAsync(session.Id, "other-profile", false, CancellationToken.None)).Value;

        Assert.False(role.IsHost);
    }

    [Fact]
    public async Task AnAnonymousViewer_IsNeitherHostNorAdmin()
    {
        var session = _sessions.AddSession("host-profile");

        var role = (await _service.GetViewerRoleAsync(session.Id, null, false, CancellationToken.None)).Value;

        Assert.False(role.IsHost);
        Assert.False(role.IsAdmin);
    }

    [Fact]
    public async Task ASessionWithoutAHost_HasNoHostViewer()
    {
        var session = _sessions.AddSession();

        var role = (await _service.GetViewerRoleAsync(session.Id, "any-profile", false, CancellationToken.None)).Value;

        Assert.False(role.IsHost);
    }

    [Fact]
    public async Task AnAdmin_IsAdmin()
    {
        var session = _sessions.AddSession("host-profile");

        var role = (await _service.GetViewerRoleAsync(session.Id, null, true, CancellationToken.None)).Value;

        Assert.True(role.IsAdmin);
        Assert.False(role.IsHost);
    }

    [Fact]
    public async Task AnUnknownSession_IsNotFound()
    {
        var result = await _service.GetViewerRoleAsync(Guid.NewGuid(), null, false, CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }
}
