using PartyKing.Domain.Repositories;
using ErrorOr;
using PartyKing.Application.Mappings;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Contract.V1.Sessions;
using PartyKing.Domain.Entities.Sessions;
using PartyKing.Domain.Enums;

namespace PartyKing.Application.Sessions.Services;

public interface ISessionService
{
    Task<IReadOnlyList<SessionDto>> GetAllAsync(CancellationToken cancellationToken);
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

    public SessionService(
        ISessionsRepository sessionsRepository,
        ISessionAuthorizer authorizer)
        : base(authorizer)
    {
        _sessionsRepository = sessionsRepository;
    }

    public async Task<IReadOnlyList<SessionDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var sessions = await _sessionsRepository.GetAllAsync(cancellationToken);

        return sessions.Select(session => session.ToDto()).ToList();
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
