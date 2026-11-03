using PartyKing.Domain.Repositories;
using ErrorOr;
using Microsoft.Extensions.Logging;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Mappings;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Contract.V1.Sessions;
using PartyKing.Domain.Entities.Sessions;

namespace PartyKing.Application.Sessions.Services;

public interface ISessionAdminService
{
    Task<ErrorOr<SessionDto>> CreateAsync(string? name, string? password, CancellationToken cancellationToken);
    Task<ErrorOr<Success>> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Removes the host so someone else can claim the session.</summary>
    Task<ErrorOr<Success>> ReleaseAsync(Guid id, CancellationToken cancellationToken);
}

internal class SessionAdminService : ISessionAdminService
{
    private const int MaxCodeAttempts = 10;

    private readonly ISessionsRepository _sessionsRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<SessionAdminService> _logger;

    public SessionAdminService(
        ISessionsRepository sessionsRepository,
        IPasswordHasher passwordHasher,
        ILogger<SessionAdminService> logger)
    {
        _sessionsRepository = sessionsRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<ErrorOr<SessionDto>> CreateAsync(
        string? name,
        string? password,
        CancellationToken cancellationToken)
    {
        if (SessionNamePolicy.Validate(name, allowBlank: true) is { } invalidName)
        {
            return invalidName;
        }

        if (SessionPasswordPolicy.Validate(password) is { } invalid)
        {
            return invalid;
        }

        var passwordHash = _passwordHasher.Hash(password!, PasswordHasher.SessionIterations);

        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var session = new Session(SessionCodeGenerator.Generate(), name ?? string.Empty, passwordHash);

            if (await _sessionsRepository.TryAddAsync(session, cancellationToken))
            {
                _logger.LogInformation("Created session {SessionId} with code {Code}", session.Id, session.Code);
                return session.ToDto();
            }
        }

        return Error.Failure(description: "Could not generate a unique session code");
    }

    public async Task<ErrorOr<Success>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        // The player, the host's token, the slideshow and the fallback playlist are cleaned up by the SessionDeleted handlers
        var deleted = await _sessionsRepository.DeleteAsync(id, cancellationToken);

        return deleted ? Result.Success : SessionErrors.NotFound();
    }

    public async Task<ErrorOr<Success>> ReleaseAsync(Guid id, CancellationToken cancellationToken)
    {
        // The player and the host's token are forgotten by the HostReleased handlers
        var released = await _sessionsRepository.ReleaseAsync(id, cancellationToken);

        return released ? Result.Success : SessionErrors.NotFound();
    }
}
