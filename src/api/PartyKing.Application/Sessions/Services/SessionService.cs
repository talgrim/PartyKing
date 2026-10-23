using ErrorOr;
using PartyKing.Contract.V1.Sessions;
using PartyKing.Domain.Entities;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Sessions.Services;

public interface ISessionService
{
    Task<IReadOnlyList<SessionDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<ErrorOr<SessionDto>> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>What the viewer may do in the session; <paramref name="profileId"/> is the logged-in host, if any.</summary>
    Task<ErrorOr<ViewerRoleDto>> GetViewerRoleAsync(
        Guid id,
        string? profileId,
        bool isAdmin,
        CancellationToken cancellationToken);
    Task<ErrorOr<SessionDto>> CreateAsync(string? name, CancellationToken cancellationToken);
    Task<ErrorOr<Success>> RenameAsync(Guid id, string? name, CancellationToken cancellationToken);
    Task<ErrorOr<Success>> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Removes the host so someone else can claim the session.</summary>
    Task<ErrorOr<Success>> ReleaseAsync(Guid id, CancellationToken cancellationToken);
}

internal class SessionService : ISessionService
{
    private const int MaxCodeAttempts = 10;
    private const int MaxNameLength = 60;

    private readonly ISessionsRepository _sessionsRepository;

    public SessionService(ISessionsRepository sessionsRepository)
    {
        _sessionsRepository = sessionsRepository;
    }

    public async Task<IReadOnlyList<SessionDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var sessions = await _sessionsRepository.GetAllAsync(cancellationToken);

        return sessions.Select(ToDto).ToList();
    }

    public async Task<ErrorOr<SessionDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(id, cancellationToken);

        return session is null ? NotFound() : ToDto(session);
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
            return NotFound();
        }

        var isHost = profileId is not null && session.HostProfileId == profileId;

        return new ViewerRoleDto(isHost, isAdmin);
    }

    public async Task<ErrorOr<SessionDto>> CreateAsync(string? name, CancellationToken cancellationToken)
    {
        if (name is { Length: > MaxNameLength })
        {
            return NameTooLong();
        }

        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var session = new Session(SessionCodeGenerator.Generate(), name ?? string.Empty);

            if (await _sessionsRepository.TryAddAsync(session, cancellationToken))
            {
                return ToDto(session);
            }
        }

        return Error.Failure(description: "Could not generate a unique session code");
    }

    public async Task<ErrorOr<Success>> RenameAsync(Guid id, string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation(description: "The session name must not be empty");
        }

        if (name.Trim().Length > MaxNameLength)
        {
            return NameTooLong();
        }

        var renamed = await _sessionsRepository.RenameAsync(id, name.Trim(), cancellationToken);

        return renamed ? Result.Success : NotFound();
    }

    public async Task<ErrorOr<Success>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _sessionsRepository.DeleteAsync(id, cancellationToken);

        return deleted ? Result.Success : NotFound();
    }

    public async Task<ErrorOr<Success>> ReleaseAsync(Guid id, CancellationToken cancellationToken)
    {
        var released = await _sessionsRepository.ReleaseAsync(id, cancellationToken);

        return released ? Result.Success : NotFound();
    }

    private static SessionDto ToDto(Session session)
    {
        return new SessionDto(
            session.Id,
            session.Code,
            session.Name,
            session.HostProfile?.DisplayName ?? session.HostProfileId,
            session.HostProfileId is not null);
    }

    private static Error NotFound() => Error.NotFound(description: "The session was not found");

    private static Error NameTooLong() =>
        Error.Validation(description: $"The session name must be at most {MaxNameLength} characters");
}
