using ErrorOr;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Sessions;

namespace PartyKing.Application.Tests;

public class SessionAccessServiceTests
{
    private readonly FakeSessionsRepository _sessions = new();
    private readonly SessionAccessService _access;
    private readonly SessionAdminService _admin;

    public SessionAccessServiceTests()
    {
        _access = SessionTestServices.Access(_sessions);
        _admin = SessionTestServices.Admin(_sessions);
    }

    [Fact]
    public async Task ACreatedSession_CanBeJoinedWithItsPassword()
    {
        var session = (await _admin.CreateAsync("Party", "secret", CancellationToken.None)).Value;

        var stamp = await _access.JoinAsync(session.Id, "secret", CancellationToken.None);

        Assert.False(stamp.IsError);
        Assert.True(await _access.HasAccessAsync(session.Id, stamp.Value, CancellationToken.None));
    }

    [Fact]
    public async Task JoiningWithAWrongPassword_IsUnauthorized()
    {
        var session = (await _admin.CreateAsync("Party", "secret", CancellationToken.None)).Value;

        var result = await _access.JoinAsync(session.Id, "wrong", CancellationToken.None);

        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
    }

    [Fact]
    public async Task JoiningAnUnknownSession_IsUnauthorizedToo()
    {
        var result = await _access.JoinAsync(Guid.NewGuid(), "secret", CancellationToken.None);

        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
    }

    [Fact]
    public async Task JoiningWithAnOverlongPassword_IsUnauthorized()
    {
        var session = (await _admin.CreateAsync("Party", "secret", CancellationToken.None)).Value;

        var result = await _access.JoinAsync(session.Id, new string('a', 101), CancellationToken.None);

        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("made-up")]
    public async Task AMissingOrForgedStamp_GivesNoAccess(string? stamp)
    {
        var session = (await _admin.CreateAsync("Party", "secret", CancellationToken.None)).Value;

        Assert.False(await _access.HasAccessAsync(session.Id, stamp, CancellationToken.None));
    }

    [Fact]
    public async Task AStampOfAnotherSession_GivesNoAccess()
    {
        var first = (await _admin.CreateAsync("One", "secret", CancellationToken.None)).Value;
        var second = (await _admin.CreateAsync("Two", "secret", CancellationToken.None)).Value;
        var stamp = (await _access.JoinAsync(first.Id, "secret", CancellationToken.None)).Value;

        Assert.False(await _access.HasAccessAsync(second.Id, stamp, CancellationToken.None));
    }

    [Fact]
    public async Task ChangingThePassword_InvalidatesEarlierUnlocks()
    {
        var session = (await _admin.CreateAsync("Party", "secret", CancellationToken.None)).Value;
        var oldStamp = (await _access.JoinAsync(session.Id, "secret", CancellationToken.None)).Value;

        var changed = await _access.ChangePasswordAsync(session.Id, "new-secret", new SessionCaller(null, true), CancellationToken.None);

        Assert.False(changed.IsError);
        Assert.False(await _access.HasAccessAsync(session.Id, oldStamp, CancellationToken.None));
        Assert.True((await _access.JoinAsync(session.Id, "secret", CancellationToken.None)).IsError);

        var newStamp = await _access.JoinAsync(session.Id, "new-secret", CancellationToken.None);
        Assert.True(await _access.HasAccessAsync(session.Id, newStamp.Value, CancellationToken.None));
    }

    [Fact]
    public async Task ChangingThePassword_ValidatesIt()
    {
        var session = (await _admin.CreateAsync("Party", "secret", CancellationToken.None)).Value;

        var result = await _access.ChangePasswordAsync(session.Id, "abc", new SessionCaller(null, true), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task ChangingThePasswordOfAnUnknownSession_IsNotFound()
    {
        var result = await _access.ChangePasswordAsync(Guid.NewGuid(), "secret", new SessionCaller(null, true), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public async Task TheHost_ChangingThePassword_GetsTheStampOfTheNewOne()
    {
        var id = _sessions.AddSession("host-1", new PasswordHasher().Hash("secret")).Id;

        var result = await _access.ChangePasswordAsync(id, "new-secret", new SessionCaller("host-1", false), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.True(await _access.HasAccessAsync(id, result.Value, CancellationToken.None));
    }

    [Fact]
    public async Task ChangingThePassword_AsAGuest_IsForbiddenAndKeepsTheOldOne()
    {
        var id = _sessions.AddSession("host-1", new PasswordHasher().Hash("secret")).Id;

        var result = await _access.ChangePasswordAsync(id, "new-secret", new SessionCaller(null, false), CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.False((await _access.JoinAsync(id, "secret", CancellationToken.None)).IsError);
    }

    [Fact]
    public async Task TheDummyHash_IsComputedOnceNoMatterHowManyServicesAreBuilt()
    {
        var hasher = new CountingPasswordHasher();
        var dummyHash = new SessionDummyHash(hasher);
        var id = _sessions.AddSession("host-1", new PasswordHasher().Hash("secret")).Id;

        for (var i = 0; i < 3; i++)
        {
            var access = SessionTestServices.Access(_sessions, hasher, dummyHash);

            await access.HasAccessAsync(id, "stamp", CancellationToken.None);
            await access.JoinAsync(Guid.NewGuid(), "secret", CancellationToken.None);
        }

        Assert.Equal(1, hasher.HashCalls);
    }

    [Fact]
    public async Task TheDummyHash_IsNotComputedBeforeSomeoneJoinsAnUnknownSession()
    {
        var hasher = new CountingPasswordHasher();
        var access = SessionTestServices.Access(_sessions, hasher);
        var id = _sessions.AddSession("host-1", new PasswordHasher().Hash("secret")).Id;

        await access.HasAccessAsync(id, "stamp", CancellationToken.None);
        await access.JoinAsync(id, "secret", CancellationToken.None);

        Assert.Equal(0, hasher.HashCalls);
    }

    private sealed class CountingPasswordHasher : IPasswordHasher
    {
        private readonly PasswordHasher _inner = new();

        public int HashCalls { get; private set; }

        public string Hash(string password, int iterations = PasswordHasher.AdminIterations)
        {
            HashCalls++;
            return _inner.Hash(password, iterations);
        }

        public bool IsHash(string value) => _inner.IsHash(value);

        public bool Verify(string password, string hash) => _inner.Verify(password, hash);
    }
}
