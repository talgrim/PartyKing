using ErrorOr;
using PartyKing.Application.Sessions.Services;

namespace PartyKing.Application.Sessions.Helpers;

internal static class SessionAuthorizerExtensions
{
    /// <summary>Runs <paramref name="action"/> only for the session's host or an admin; any other outcome of the check is the result.</summary>
    public static async Task<ErrorOr<T>> RunAsManagerAsync<T>(
        this ISessionAuthorizer authorizer,
        Guid sessionId,
        SessionCaller caller,
        Func<Task<ErrorOr<T>>> action,
        CancellationToken cancellationToken)
    {
        var authorized = await authorizer.AuthorizeManagerAsync(sessionId, caller, cancellationToken);

        if (authorized.IsError)
        {
            return authorized.Errors;
        }

        return await action();
    }
}
