using ErrorOr;

namespace PartyKing.Application.Sessions.Services;

/// <summary>
/// Base of the services that have actions only the session's host or an admin may run, so none of them repeats the
/// authorization or splits an action into a public method and a private one that holds the work.
/// </summary>
internal abstract class ManagerGuardedService
{
    private readonly ISessionAuthorizer _sessionAuthorizer;

    protected ManagerGuardedService(ISessionAuthorizer sessionAuthorizer)
    {
        _sessionAuthorizer = sessionAuthorizer;
    }

    /// <summary>Runs <paramref name="action"/> only for the session's host or an admin; any other outcome of the check is the result.</summary>
    protected async Task<ErrorOr<T>> RunAsManagerAsync<T>(
        Guid sessionId,
        SessionCaller caller,
        Func<Task<ErrorOr<T>>> action,
        CancellationToken cancellationToken)
    {
        var authorized = await _sessionAuthorizer.AuthorizeManagerAsync(sessionId, caller, cancellationToken);

        if (authorized.IsError)
        {
            return authorized.Errors;
        }

        return await action();
    }
}
