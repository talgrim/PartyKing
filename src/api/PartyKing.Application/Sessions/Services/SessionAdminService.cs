using PartyKing.Domain.Repositories;
using ErrorOr;
using Microsoft.Extensions.Logging;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Mappings;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Application.Slideshow.Services;
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
    private readonly ISlideshowService _slideshowService;
    private readonly IHostSessionCleanup _hostCleanup;
    private readonly ILogger<SessionAdminService> _logger;

    public SessionAdminService(
        ISessionsRepository sessionsRepository,
        IPasswordHasher passwordHasher,
        ISlideshowService slideshowService,
        IHostSessionCleanup hostCleanup,
        ILogger<SessionAdminService> logger)
    {
        _sessionsRepository = sessionsRepository;
        _passwordHasher = passwordHasher;
        _slideshowService = slideshowService;
        _hostCleanup = hostCleanup;
        _logger = logger;
    }

    public async Task<ErrorOr<SessionDto>> CreateAsync(
        string? name,
        string? password,
        CancellationToken cancellationToken)
    {
        if (name is { Length: > SessionNamePolicy.MaxLength })
        {
            return SessionNamePolicy.TooLong();
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
        var hostProfileId = (await _sessionsRepository.GetAsync(id, cancellationToken))?.HostProfileId;

        var deleted = await _sessionsRepository.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return SessionErrors.NotFound();
        }

        await _hostCleanup.ForgetHostAsync(id, hostProfileId, cancellationToken);
        _logger.LogInformation("Deleted session {SessionId}", id);

        // The rows went with the cascade delete; the uploaded files are still on disk
        await _slideshowService.DeleteSessionAsync(id);

        return Result.Success;
    }

    public async Task<ErrorOr<Success>> ReleaseAsync(Guid id, CancellationToken cancellationToken)
    {
        var hostProfileId = (await _sessionsRepository.GetAsync(id, cancellationToken))?.HostProfileId;

        var released = await _sessionsRepository.ReleaseAsync(id, cancellationToken);

        if (!released)
        {
            return SessionErrors.NotFound();
        }

        await _hostCleanup.ForgetHostAsync(id, hostProfileId, cancellationToken);
        _logger.LogInformation("Released the host of session {SessionId}", id);

        return Result.Success;
    }
}
