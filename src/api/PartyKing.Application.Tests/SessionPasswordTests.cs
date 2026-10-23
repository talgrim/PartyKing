using ErrorOr;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Player.Services;
using PartyKing.Application.Sessions.Services;

namespace PartyKing.Application.Tests;

public class SessionPasswordTests
{
    private readonly FakeSessionsRepository _sessions = new();
    private readonly SessionService _service;

    public SessionPasswordTests()
    {
        _service = new SessionService(_sessions, new PasswordHasher(), new PlayerStateStore(new FakeClock()), new FakeHostTokenProvider());
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

    [Fact]
    public async Task CreatingASession_WithAnOverlongPassword_IsRejected()
    {
        var result = await _service.CreateAsync("Party", new string('a', 101), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task ACreatedSession_CanBeJoinedWithItsPassword()
    {
        var session = (await _service.CreateAsync("Party", "secret", CancellationToken.None)).Value;

        var stamp = await _service.JoinAsync(session.Id, "secret", CancellationToken.None);

        Assert.False(stamp.IsError);
        Assert.True(await _service.HasAccessAsync(session.Id, stamp.Value, CancellationToken.None));
    }

    [Fact]
    public async Task JoiningWithAWrongPassword_IsUnauthorized()
    {
        var session = (await _service.CreateAsync("Party", "secret", CancellationToken.None)).Value;

        var result = await _service.JoinAsync(session.Id, "wrong", CancellationToken.None);

        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
    }

    [Fact]
    public async Task JoiningAnUnknownSession_IsUnauthorizedToo()
    {
        var result = await _service.JoinAsync(Guid.NewGuid(), "secret", CancellationToken.None);

        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
    }

    [Fact]
    public async Task JoiningWithAnOverlongPassword_IsUnauthorized()
    {
        var session = (await _service.CreateAsync("Party", "secret", CancellationToken.None)).Value;

        var result = await _service.JoinAsync(session.Id, new string('a', 101), CancellationToken.None);

        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("made-up")]
    public async Task AMissingOrForgedStamp_GivesNoAccess(string? stamp)
    {
        var session = (await _service.CreateAsync("Party", "secret", CancellationToken.None)).Value;

        Assert.False(await _service.HasAccessAsync(session.Id, stamp, CancellationToken.None));
    }

    [Fact]
    public async Task AStampOfAnotherSession_GivesNoAccess()
    {
        var first = (await _service.CreateAsync("One", "secret", CancellationToken.None)).Value;
        var second = (await _service.CreateAsync("Two", "secret", CancellationToken.None)).Value;
        var stamp = (await _service.JoinAsync(first.Id, "secret", CancellationToken.None)).Value;

        Assert.False(await _service.HasAccessAsync(second.Id, stamp, CancellationToken.None));
    }

    [Fact]
    public async Task ChangingThePassword_InvalidatesEarlierUnlocks()
    {
        var session = (await _service.CreateAsync("Party", "secret", CancellationToken.None)).Value;
        var oldStamp = (await _service.JoinAsync(session.Id, "secret", CancellationToken.None)).Value;

        var changed = await _service.ChangePasswordAsync(session.Id, "new-secret", CancellationToken.None);

        Assert.False(changed.IsError);
        Assert.False(await _service.HasAccessAsync(session.Id, oldStamp, CancellationToken.None));
        Assert.True((await _service.JoinAsync(session.Id, "secret", CancellationToken.None)).IsError);

        var newStamp = await _service.JoinAsync(session.Id, "new-secret", CancellationToken.None);
        Assert.True(await _service.HasAccessAsync(session.Id, newStamp.Value, CancellationToken.None));
    }

    [Fact]
    public async Task ChangingThePassword_ValidatesIt()
    {
        var session = (await _service.CreateAsync("Party", "secret", CancellationToken.None)).Value;

        var result = await _service.ChangePasswordAsync(session.Id, "abc", CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task ChangingThePasswordOfAnUnknownSession_IsNotFound()
    {
        var result = await _service.ChangePasswordAsync(Guid.NewGuid(), "secret", CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }
}
