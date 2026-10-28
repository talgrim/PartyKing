using Microsoft.Extensions.Logging.Abstractions;
using ErrorOr;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Player.Services;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;

namespace PartyKing.Application.Tests;

public class SessionSettingsAuthorizationTests
{
    private const string HostId = "host-1";

    private readonly FakeSessionsRepository _sessions = new();
    private readonly SessionService _service;
    private readonly Guid _id;

    public SessionSettingsAuthorizationTests()
    {
        var clock = new FakeClock();
        _service = new SessionService(
            _sessions,
            new PasswordHasher(),
            new PlayerStateStore(clock),
            new FakeHostTokenProvider(),
            new FakeSlideshowService(),
            JoinTokens.Create(clock),
            clock,
            new NullSessionMusicNotifier(),
            new NullPlayerConnectionWatchdog(),
            NullLogger<SessionService>.Instance);
        _id = _sessions.AddSession(HostId, new PasswordHasher().Hash("secret")).Id;
    }

    [Theory]
    [InlineData(HostId, false)]
    [InlineData(null, true)]
    public async Task TheHostAndAnAdmin_CanManageTheSession(string? profileId, bool isAdmin)
    {
        var result = await _service.AuthorizeManagerAsync(_id, profileId, isAdmin, CancellationToken.None);

        Assert.False(result.IsError);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("someone-else")]
    public async Task AnyoneElse_IsForbidden(string? profileId)
    {
        var result = await _service.AuthorizeManagerAsync(_id, profileId, false, CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
    }

    [Fact]
    public async Task AnUnknownSession_IsNotFound()
    {
        var result = await _service.AuthorizeManagerAsync(Guid.NewGuid(), HostId, true, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
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

    [Fact]
    public async Task TheHost_ChangingThePassword_GetsTheStampOfTheNewOne()
    {
        var result = await _service.ChangePasswordAsync(_id, "new-secret", HostId, false, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.True(await _service.HasAccessAsync(_id, result.Value, CancellationToken.None));
    }

    [Fact]
    public async Task ChangingThePassword_AsAGuest_IsForbiddenAndKeepsTheOldOne()
    {
        var result = await _service.ChangePasswordAsync(_id, "new-secret", null, false, CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.False((await _service.JoinAsync(_id, "secret", CancellationToken.None)).IsError);
    }
}
