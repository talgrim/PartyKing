using ErrorOr;
using PartyKing.Application.Player.Services;
using PartyKing.Application.Sessions.Services;

namespace PartyKing.Application.Tests;

public class SessionAdminServiceTests
{
    private readonly FakeSessionsRepository _sessions = new();
    private readonly PlayerStateStore _playerStates = new(new FakeClock());
    private readonly FakeHostTokenProvider _tokens = new();
    private readonly FakeSlideshowService _slideshows = new();
    private readonly SessionAdminService _service;

    public SessionAdminServiceTests()
    {
        _service = SessionTestServices.Admin(_sessions, _playerStates, _tokens, _slideshows);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public async Task CreatingASession_WithoutAValidPassword_IsRejected(string? password)
    {
        var result = await _service.CreateAsync("Party", password, CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreatingASession_WithoutAName_UsesTheDefaultName(string? name)
    {
        var result = await _service.CreateAsync(name, "secret", CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("New session", result.Value.Name);
    }

    [Fact]
    public async Task CreatingASession_TrimsTheNameBeforeCheckingItsLength()
    {
        var name = $"  {new string('a', 60)}  ";

        var result = await _service.CreateAsync(name, "secret", CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(new string('a', 60), result.Value.Name);
    }

    [Fact]
    public async Task CreatingASession_WithAnOverlongName_IsRejected()
    {
        var result = await _service.CreateAsync(new string('a', 61), "secret", CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task CreatingASession_WithAnOverlongPassword_IsRejected()
    {
        var result = await _service.CreateAsync("Party", new string('a', 101), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task DeletingASession_ForgetsItsPlayerAndTheHostsToken()
    {
        var session = _sessions.AddSession("host-profile");
        _playerStates.Report(session.Id, 1, paused: false);

        var result = await _service.DeleteAsync(session.Id, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.False(_playerStates.Get(session.Id).Connected);
        Assert.Equal(["host-profile"], _tokens.Invalidated);
    }

    [Fact]
    public async Task DeletingASession_DeletesItsSlideshow()
    {
        var session = _sessions.AddSession();

        var result = await _service.DeleteAsync(session.Id, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal([session.Id], _slideshows.DeletedSessions);
    }

    [Fact]
    public async Task DeletingAnUnknownSession_KeepsEverySlideshow()
    {
        var result = await _service.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Empty(_slideshows.DeletedSessions);
    }

    [Fact]
    public async Task ReleasingTheHost_ForgetsItsPlayerAndTheHostsToken()
    {
        var session = _sessions.AddSession("host-profile");
        _playerStates.Report(session.Id, 1, paused: false);

        var result = await _service.ReleaseAsync(session.Id, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.False(_playerStates.Get(session.Id).Connected);
        Assert.Equal(["host-profile"], _tokens.Invalidated);
    }

    [Fact]
    public async Task DeletingASessionWithoutAHost_InvalidatesNoToken()
    {
        var session = _sessions.AddSession();

        await _service.DeleteAsync(session.Id, CancellationToken.None);

        Assert.Empty(_tokens.Invalidated);
    }

    [Fact]
    public async Task DeletingAnUnknownSession_IsNotFoundAndInvalidatesNothing()
    {
        var result = await _service.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Empty(_tokens.Invalidated);
    }
}
