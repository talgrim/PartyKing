using Microsoft.Extensions.Logging.Abstractions;
using ErrorOr;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Player.Services;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;

namespace PartyKing.Application.Tests;

public class SessionJoinTokenTests
{
    private const string HostId = "host-1";

    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeClock _clock = new();
    private readonly SessionService _service;

    public SessionJoinTokenTests()
    {
        _service = new SessionService(
            _sessions,
            new PasswordHasher(),
            new PlayerStateStore(_clock),
            new FakeHostTokenProvider(),
            new FakeSlideshowService(),
            JoinTokens.Create(_clock),
            _clock,
            new NullSessionMusicNotifier(),
            new NullPlayerConnectionWatchdog(),
            NullLogger<SessionService>.Instance);
    }

    private Task<Guid> CreateSessionAsync() =>
        Task.FromResult(_sessions.AddSession(HostId, new PasswordHasher().Hash("secret")).Id);

    [Fact]
    public async Task ATokenFromTheHost_UnlocksTheSession()
    {
        var id = await CreateSessionAsync();
        var token = (await _service.CreateJoinTokenAsync(id, HostId, false, CancellationToken.None)).Value;

        var stamp = await _service.JoinWithTokenAsync(id, token.Token, CancellationToken.None);

        Assert.False(stamp.IsError);
        Assert.True(await _service.HasAccessAsync(id, stamp.Value, CancellationToken.None));
    }

    [Fact]
    public async Task AnAdmin_CanCreateAToken()
    {
        var id = await CreateSessionAsync();

        var result = await _service.CreateJoinTokenAsync(id, null, true, CancellationToken.None);

        Assert.False(result.IsError);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("someone-else")]
    public async Task ANonHostNonAdmin_CannotCreateAToken(string? profileId)
    {
        var id = await CreateSessionAsync();

        var result = await _service.CreateJoinTokenAsync(id, profileId, false, CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
    }

    [Fact]
    public async Task CreatingATokenForAnUnknownSession_IsNotFound()
    {
        var result = await _service.CreateJoinTokenAsync(Guid.NewGuid(), null, true, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task AnExpiredToken_IsUnauthorized()
    {
        var id = await CreateSessionAsync();
        var token = (await _service.CreateJoinTokenAsync(id, HostId, false, CancellationToken.None)).Value;

        _clock.UtcNow = token.ExpiresAt;

        var result = await _service.JoinWithTokenAsync(id, token.Token, CancellationToken.None);

        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
    }

    [Fact]
    public async Task ATokenStillWorks_JustBeforeItExpires()
    {
        var id = await CreateSessionAsync();
        var token = (await _service.CreateJoinTokenAsync(id, HostId, false, CancellationToken.None)).Value;

        _clock.UtcNow = token.ExpiresAt.AddSeconds(-1);

        Assert.False((await _service.JoinWithTokenAsync(id, token.Token, CancellationToken.None)).IsError);
    }

    [Fact]
    public async Task ATokenForAnotherSession_IsUnauthorized()
    {
        var id = await CreateSessionAsync();
        var other = await CreateSessionAsync();
        var token = (await _service.CreateJoinTokenAsync(id, HostId, false, CancellationToken.None)).Value;

        var result = await _service.JoinWithTokenAsync(other, token.Token, CancellationToken.None);

        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
    }

    [Fact]
    public async Task ChangingThePassword_RevokesOutstandingTokens()
    {
        var id = await CreateSessionAsync();
        var token = (await _service.CreateJoinTokenAsync(id, HostId, false, CancellationToken.None)).Value;

        await _service.ChangePasswordAsync(id, "new-secret", HostId, false, CancellationToken.None);
        var result = await _service.JoinWithTokenAsync(id, token.Token, CancellationToken.None);

        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task AMissingOrForgedToken_IsUnauthorized(string? token)
    {
        var id = await CreateSessionAsync();

        var result = await _service.JoinWithTokenAsync(id, token, CancellationToken.None);

        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
    }

    [Fact]
    public async Task ATokenForAnUnknownSession_IsUnauthorized()
    {
        var result = await _service.JoinWithTokenAsync(Guid.NewGuid(), "x", CancellationToken.None);

        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
    }
}
