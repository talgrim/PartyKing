using ErrorOr;
using Microsoft.Extensions.Logging;
using PartyKing.Application.Mappings;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Contract.V1.Sessions;
using PartyKing.Infrastructure.Repositories;

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

internal class SessionService : ISessionService
{
    private readonly ISessionsRepository _sessionsRepository;
    private readonly ISessionAuthorizer _authorizer;
    private readonly ILogger<SessionService> _logger;

    public SessionService(
        ISessionsRepository sessionsRepository,
        ISessionAuthorizer authorizer,
        ILogger<SessionService> logger)
    {
        _sessionsRepository = sessionsRepository;
        _authorizer = authorizer;
        _logger = logger;
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

    public async Task<ErrorOr<Success>> RenameAsync(
        Guid id,
        string? name,
        SessionCaller caller,
        CancellationToken cancellationToken)
    {
        if (await _authorizer.AuthorizeManagerAsync(id, caller, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation(description: "The session name must not be empty");
        }

        if (name.Trim().Length > SessionNamePolicy.MaxLength)
        {
            return SessionNamePolicy.TooLong();
        }

        var renamed = await _sessionsRepository.RenameAsync(id, name.Trim(), cancellationToken);

        if (renamed)
        {
            _logger.LogInformation("Renamed session {SessionId}", id);
        }

        return renamed ? Result.Success : SessionErrors.NotFound();
    }
}
