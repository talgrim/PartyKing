using PartyKing.Domain.Repositories;
using ErrorOr;
using PartyKing.Application.Demo.Services;
using PartyKing.Application.Mappings;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Contract.V1.Sessions;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Sessions.Services;

public interface ISessionService
{
    /// <summary>The public list: whether each session has a host, not who it is. Empty in the demo.</summary>
    Task<IReadOnlyList<SessionListItemDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<ErrorOr<SessionDto>> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Renames the session. Only the host (<paramref name="caller"/>) or an admin may; anyone else gets Forbidden.</summary>
    Task<ErrorOr<Success>> RenameAsync(
        Guid id,
        string? name,
        SessionCaller caller,
        CancellationToken cancellationToken);
}

internal class SessionService : ManagerGuardedService, ISessionService
{
    private readonly ISessionsRepository _sessionsRepository;
    private readonly IDemoMode _demoMode;

    public SessionService(
        ISessionsRepository sessionsRepository,
        ISessionAuthorizer authorizer,
        IDemoMode demoMode)
        : base(authorizer)
    {
        _sessionsRepository = sessionsRepository;
        _demoMode = demoMode;
    }

    public async Task<IReadOnlyList<SessionListItemDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        // The sessions of the other visitors are none of a visitor's business
        if (_demoMode.IsEnabled)
        {
            return [];
        }

        var sessions = await _sessionsRepository.GetAllAsync(cancellationToken);

        return sessions.Select(session => session.ToListItemDto()).ToList();
    }

    public async Task<ErrorOr<SessionDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(id, cancellationToken);

        return session is null ? SessionErrors.NotFound() : session.ToDto();
    }

    public Task<ErrorOr<Success>> RenameAsync(
        Guid id,
        string? name,
        SessionCaller caller,
        CancellationToken cancellationToken)
    {
        return RunAsManagerAsync(
            id,
            caller,
            RenameAsyncCore,
            cancellationToken);

        async Task<ErrorOr<Success>> RenameAsyncCore()
        {
            var result = await _sessionsRepository.RenameAsync(id, name, cancellationToken);

            return result switch
            {
                SessionRenameResult.Renamed => Result.Success,
                SessionRenameResult.NameTooLong => SessionNamePolicy.ToError(SessionNameFailure.TooLong),
                SessionRenameResult.NameEmpty => SessionNamePolicy.ToError(SessionNameFailure.Empty),
                _ => SessionErrors.NotFound()
            };
        }
    }
}
