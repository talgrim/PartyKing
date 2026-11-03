using ErrorOr;
using PartyKing.Application.Sessions;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Sessions;

namespace PartyKing.Application.Tests;

public class SessionAuthorizerExtensionsTests
{
    private static readonly SessionCaller Caller = new(null, false);

    [Fact]
    public async Task RunAsManagerAsync_RunsTheActionWhenAuthorized()
    {
        var authorizer = new StubAuthorizer(Result.Success);

        var result = await authorizer.RunAsManagerAsync(
            Guid.NewGuid(),
            Caller,
            () => Task.FromResult<ErrorOr<int>>(42),
            CancellationToken.None);

        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task RunAsManagerAsync_ReturnsTheDenialAndSkipsTheActionOtherwise()
    {
        var authorizer = new StubAuthorizer(Error.Forbidden(description: "no"));
        var ran = false;

        var result = await authorizer.RunAsManagerAsync(
            Guid.NewGuid(),
            Caller,
            () =>
            {
                ran = true;
                return Task.FromResult<ErrorOr<int>>(42);
            },
            CancellationToken.None);

        Assert.False(ran);
        Assert.Equal(ErrorType.Forbidden, result.FirstError.Type);
        Assert.Equal("no", result.FirstError.Description);
    }

    private sealed class StubAuthorizer(ErrorOr<Success> outcome) : ISessionAuthorizer
    {
        public Task<ErrorOr<Success>> AuthorizeManagerAsync(
            Guid id,
            SessionCaller caller,
            CancellationToken cancellationToken) => Task.FromResult(outcome);

        public Task<ErrorOr<ViewerRoleDto>> GetViewerRoleAsync(
            Guid id,
            SessionCaller caller,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
