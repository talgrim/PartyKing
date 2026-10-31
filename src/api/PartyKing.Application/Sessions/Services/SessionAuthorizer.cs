using ErrorOr;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Contract.V1.Sessions;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Sessions.Services;

public interface ISessionAuthorizer
{
    /// <summary>
    /// Succeeds only for the session's host (<paramref name="profileId"/>) or an admin: NotFound for an unknown
    /// session, Forbidden for anyone else. For settings that live outside this service.
    /// </summary>
    Task<ErrorOr<Success>> AuthorizeManagerAsync(
        Guid id,
        string? profileId,
        bool isAdmin,
        CancellationToken cancellationToken);

    /// <summary>What the viewer may do in the session; <paramref name="profileId"/> is the logged-in host, if any.</summary>
    Task<ErrorOr<ViewerRoleDto>> GetViewerRoleAsync(
        Guid id,
        string? profileId,
        bool isAdmin,
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
        string? profileId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(id, cancellationToken);

        if (session is null)
        {
            return SessionErrors.NotFound();
        }

        return SessionRoles.IsManager(session, profileId, isAdmin)
            ? Result.Success
            : Error.Forbidden(description: "Only the host or an admin can change the session settings");
    }

    public async Task<ErrorOr<ViewerRoleDto>> GetViewerRoleAsync(
        Guid id,
        string? profileId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(id, cancellationToken);

        if (session is null)
        {
            return SessionErrors.NotFound();
        }

        return new ViewerRoleDto(SessionRoles.IsHost(session, profileId), isAdmin);
    }
}
