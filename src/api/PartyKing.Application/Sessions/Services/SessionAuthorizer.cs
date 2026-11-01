using PartyKing.Domain.Repositories;
using ErrorOr;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Contract.V1.Sessions;

namespace PartyKing.Application.Sessions.Services;

public interface ISessionAuthorizer
{
    /// <summary>
    /// Succeeds only for the session's host (<paramref name="caller"/>) or an admin: NotFound for an unknown
    /// session, Forbidden for anyone else. For settings that live outside this service.
    /// </summary>
    Task<ErrorOr<Success>> AuthorizeManagerAsync(
        Guid id,
        SessionCaller caller,
        CancellationToken cancellationToken);

    /// <summary>What the viewer may do in the session; <paramref name="caller"/> carries the logged-in host, if any.</summary>
    Task<ErrorOr<ViewerRoleDto>> GetViewerRoleAsync(
        Guid id,
        SessionCaller caller,
        CancellationToken cancellationToken);
}

internal class SessionAuthorizer : ISessionAuthorizer
{
    private readonly ISessionsRepository _sessionsRepository;

    public SessionAuthorizer(ISessionsRepository sessionsRepository)
    {
        _sessionsRepository = sessionsRepository;
    }

    public async Task<ErrorOr<Success>> AuthorizeManagerAsync(
        Guid id,
        SessionCaller caller,
        CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(id, cancellationToken);

        if (session is null)
        {
            return SessionErrors.NotFound();
        }

        return SessionRoles.IsManager(session, caller)
            ? Result.Success
            : Error.Forbidden(description: "Only the host or an admin can change the session settings");
    }

    public async Task<ErrorOr<ViewerRoleDto>> GetViewerRoleAsync(
        Guid id,
        SessionCaller caller,
        CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(id, cancellationToken);

        if (session is null)
        {
            return SessionErrors.NotFound();
        }

        return new ViewerRoleDto(SessionRoles.IsHost(session, caller), caller.IsAdmin);
    }
}
