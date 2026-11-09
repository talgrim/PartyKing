using ErrorOr;
using PartyKing.Application.Sessions;
using PartyKing.Application.Sessions.Services;
using PartyKing.Contract.V1.Sessions;

namespace PartyKing.Application.Tests;

public class ManagerGuardedServiceTests
{
    private static readonly SessionCaller Caller = new(null, false);

    [Fact]
    public async Task RunAsManager_RunsTheActionWhenAuthorized()
    {
        var service = new GuardedService(new StubAuthorizer(Result.Success));

        var result = await service.RunAsync(
            Guid.NewGuid(),
            Caller,
            () => Task.FromResult<ErrorOr<int>>(42),
            CancellationToken.None);

        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task RunAsManager_ReturnsTheDenialAndSkipsTheActionOtherwise()
    {
        var service = new GuardedService(new StubAuthorizer(Error.Forbidden(description: "no")));
        var ran = false;

        var result = await service.RunAsync(
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

    private sealed class GuardedService(ISessionAuthorizer authorizer) : ManagerGuardedService(authorizer)
    {
        public Task<ErrorOr<int>> RunAsync(
            Guid sessionId,
            SessionCaller caller,
            Func<Task<ErrorOr<int>>> action,
            CancellationToken cancellationToken) => RunAsManagerAsync(sessionId, caller, action, cancellationToken);
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
