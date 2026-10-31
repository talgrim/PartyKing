using ErrorOr;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Sessions;

namespace PartyKing.Application.Tests;

public class SessionAuthorizerTests
{
    private const string HostId = "host-1";

    private readonly FakeSessionsRepository _sessions = new();
    private readonly SessionAuthorizer _authorizer;

    public SessionAuthorizerTests()
    {
        _authorizer = new SessionAuthorizer(_sessions);
    }

    [Fact]
    public async Task TheLoggedInHost_IsHost()
    {
        var session = _sessions.AddSession("host-profile");

        var role = (await _authorizer.GetViewerRoleAsync(session.Id, new SessionCaller("host-profile", false), CancellationToken.None)).Value;

        Assert.True(role.IsHost);
        Assert.False(role.IsAdmin);
    }

    [Fact]
    public async Task TheHostOfAnotherSession_IsNotHostHere()
    {
        var session = _sessions.AddSession("host-profile");

        var role = (await _authorizer.GetViewerRoleAsync(session.Id, new SessionCaller("other-profile", false), CancellationToken.None)).Value;

        Assert.False(role.IsHost);
    }

    [Fact]
    public async Task AnAnonymousViewer_IsNeitherHostNorAdmin()
    {
        var session = _sessions.AddSession("host-profile");

        var role = (await _authorizer.GetViewerRoleAsync(session.Id, new SessionCaller(null, false), CancellationToken.None)).Value;

        Assert.False(role.IsHost);
        Assert.False(role.IsAdmin);
    }

    [Fact]
    public async Task ASessionWithoutAHost_HasNoHostViewer()
    {
        var session = _sessions.AddSession();

        var role = (await _authorizer.GetViewerRoleAsync(session.Id, new SessionCaller("any-profile", false), CancellationToken.None)).Value;

        Assert.False(role.IsHost);
    }

    [Fact]
    public async Task AnAdmin_IsAdmin()
    {
        var session = _sessions.AddSession("host-profile");

        var role = (await _authorizer.GetViewerRoleAsync(session.Id, new SessionCaller(null, true), CancellationToken.None)).Value;

        Assert.True(role.IsAdmin);
        Assert.False(role.IsHost);
    }

    [Fact]
    public async Task AnUnknownSession_IsNotFound()
    {
        var result = await _authorizer.GetViewerRoleAsync(Guid.NewGuid(), new SessionCaller(null, false), CancellationToken.None);

        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Theory]
    [InlineData(HostId, false)]
    [InlineData(null, true)]
    public async Task TheHostAndAnAdmin_CanManageTheSession(string? profileId, bool isAdmin)
    {
        var id = _sessions.AddSession(HostId).Id;

        var result = await _authorizer.AuthorizeManagerAsync(id, new SessionCaller(profileId, isAdmin), CancellationToken.None);

        Assert.False(result.IsError);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("someone-else")]
    public async Task AnyoneElse_IsForbiddenFromManagingTheSession(string? profileId)
    {
        var id = _sessions.AddSession(HostId).Id;

        var result = await _authorizer.AuthorizeManagerAsync(id, new SessionCaller(profileId, false), CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
    }

    [Fact]
    public async Task ManagingAnUnknownSession_IsNotFound()
    {
        var result = await _authorizer.AuthorizeManagerAsync(Guid.NewGuid(), new SessionCaller(HostId, true), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }
}
