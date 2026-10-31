using Microsoft.Extensions.Logging.Abstractions;
using ErrorOr;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Sessions.Services;

namespace PartyKing.Application.Tests;

public class SessionServiceTests
{
    private const string HostId = "host-1";

    private readonly FakeSessionsRepository _sessions = new();
    private readonly SessionService _service;
    private readonly Guid _id;

    public SessionServiceTests()
    {
        _service = new SessionService(_sessions, new SessionAuthorizer(_sessions), NullLogger<SessionService>.Instance);
        _id = _sessions.AddSession(HostId, new PasswordHasher().Hash("secret")).Id;
    }

    [Fact]
    public async Task TheHost_CanRenameTheSession()
    {
        var result = await _service.RenameAsync(_id, "New name", HostId, false, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("New name", _sessions.Renamed.Single().Name);
    }

    [Fact]
    public async Task RenamingTheSession_AsAGuest_IsForbiddenAndChangesNothing()
    {
        var result = await _service.RenameAsync(_id, "New name", "someone-else", false, CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Empty(_sessions.Renamed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task RenamingTheSession_ToABlankName_IsRejected(string? name)
    {
        var result = await _service.RenameAsync(_id, name, HostId, false, CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }
}
