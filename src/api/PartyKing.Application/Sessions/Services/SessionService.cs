using PartyKing.Domain.Repositories;
using ErrorOr;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Demo.Services;
using PartyKing.Application.Mappings;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Application.System;
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

    /// <summary>
    /// Deletes the session at once, with its queue, photos and games. Only the host (<paramref name="caller"/>) or an
    /// admin may; anyone else gets Forbidden. Refused in the demo, whose sessions end on their own.
    /// </summary>
    Task<ErrorOr<Success>> DeleteAsync(Guid id, SessionCaller caller, CancellationToken cancellationToken);

    /// <summary>
    /// Any guest with access asks for the session to be deleted after the configured delay (7 days by default); the
    /// operator is told by mail. Idempotent: asking again keeps the first time and sends nothing. Refused in the demo.
    /// </summary>
    Task<ErrorOr<Success>> RequestDeletionAsync(Guid id, CancellationToken cancellationToken);
}

internal class SessionService : ManagerGuardedService, ISessionService
{
    private readonly ISessionsRepository _sessionsRepository;
    private readonly IDemoMode _demoMode;
    private readonly IDateTimeProvider _clock;
    private readonly SessionDeletionConfiguration _deletionConfiguration;

    public SessionService(
        ISessionsRepository sessionsRepository,
        ISessionAuthorizer authorizer,
        IDemoMode demoMode,
        IDateTimeProvider clock,
        IOptions<SessionDeletionConfiguration> deletionConfiguration)
        : base(authorizer)
    {
        _sessionsRepository = sessionsRepository;
        _demoMode = demoMode;
        _clock = clock;
        _deletionConfiguration = deletionConfiguration.Value;
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

    public Task<ErrorOr<Success>> DeleteAsync(Guid id, SessionCaller caller, CancellationToken cancellationToken)
    {
        return RunAsManagerAsync(
            id,
            caller,
            DeleteAsyncCore,
            cancellationToken);

        async Task<ErrorOr<Success>> DeleteAsyncCore()
        {
            // The demo deletes a session together with its synthetic host when it expires; a delete here would leave that profile behind
            if (_demoMode.IsEnabled)
            {
                return SessionErrors.DeletionUnavailableInDemo();
            }

            // The player, the host's token, the slideshow and the fallback playlist are cleaned up by the SessionDeleted handlers
            var deleted = await _sessionsRepository.DeleteAsync(id, cancellationToken);

            return deleted ? Result.Success : SessionErrors.NotFound();
        }
    }

    public async Task<ErrorOr<Success>> RequestDeletionAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_demoMode.IsEnabled)
        {
            return SessionErrors.DeletionUnavailableInDemo();
        }

        var result = await _sessionsRepository.ScheduleDeletionAsync(
            id,
            _clock.UtcNow.UtcDateTime,
            _deletionConfiguration.Delay,
            cancellationToken);

        // The mail is sent by the handler of the event that only a new schedule raises
        return result == SessionDeletionScheduleResult.SessionNotFound ? SessionErrors.NotFound() : Result.Success;
    }
}
