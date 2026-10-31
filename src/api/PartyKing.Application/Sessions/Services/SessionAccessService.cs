using ErrorOr;
using Microsoft.Extensions.Logging;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Sessions.Services;

public interface ISessionAccessService
{
    /// <summary>
    /// Checks the password. Returns the access stamp to remember, or Unauthorized, the same for a wrong password
    /// and an unknown session.
    /// </summary>
    Task<ErrorOr<string>> JoinAsync(Guid id, string? password, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the password; everyone who had unlocked the session has to enter the new one. Only the host
    /// (<paramref name="profileId"/>) or an admin may; anyone else gets Forbidden. Returns the access stamp of the
    /// new password, so the caller can stay unlocked.
    /// </summary>
    Task<ErrorOr<string>> ChangePasswordAsync(
        Guid id,
        string? password,
        string? profileId,
        bool isAdmin,
        CancellationToken cancellationToken);

    /// <summary>True when <paramref name="accessStamp"/> was issued for the session's current password.</summary>
    Task<bool> HasAccessAsync(Guid id, string? accessStamp, CancellationToken cancellationToken);
}

internal class SessionAccessService : ISessionAccessService
{
    private readonly ISessionsRepository _sessionsRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionAuthorizer _authorizer;
    private readonly ILogger<SessionAccessService> _logger;

    private readonly SessionDummyHash _dummyHash;

    public SessionAccessService(
        ISessionsRepository sessionsRepository,
        IPasswordHasher passwordHasher,
        ISessionAuthorizer authorizer,
        SessionDummyHash dummyHash,
        ILogger<SessionAccessService> logger)
    {
        _sessionsRepository = sessionsRepository;
        _passwordHasher = passwordHasher;
        _authorizer = authorizer;
        _dummyHash = dummyHash;
        _logger = logger;
    }

    public async Task<ErrorOr<string>> JoinAsync(Guid id, string? password, CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(id, cancellationToken);

        // An unknown session is verified against the dummy hash, so it costs the same as a wrong password
        // An over-long password can't be right, and skipping the hash keeps it from being a cheap CPU burner
        var valid = password is { Length: <= SessionPasswordPolicy.MaxLength }
            && _passwordHasher.Verify(password, session?.PasswordHash ?? _dummyHash.Value);

        if (session is null || !valid)
        {
            _logger.LogWarning("Failed to join session {SessionId}: wrong password", id);
            return Error.Unauthorized(description: "Wrong session password");
        }

        _logger.LogDebug("Joined session {SessionId} with the password", id);
        return SessionAccessStamp.For(session.PasswordHash);
    }

    public async Task<ErrorOr<string>> ChangePasswordAsync(
        Guid id,
        string? password,
        string? profileId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        if (await _authorizer.AuthorizeManagerAsync(id, profileId, isAdmin, cancellationToken) is { IsError: true } denied)
        {
            return denied.Errors;
        }

        if (SessionPasswordPolicy.Validate(password) is { } invalid)
        {
            return invalid;
        }

        var passwordHash = _passwordHasher.Hash(password!, PasswordHasher.SessionIterations);
        var updated = await _sessionsRepository.UpdatePasswordHashAsync(id, passwordHash, cancellationToken);

        if (updated)
        {
            _logger.LogInformation("Changed the password of session {SessionId}", id);
        }

        return updated ? SessionAccessStamp.For(passwordHash) : SessionErrors.NotFound();
    }

    public async Task<bool> HasAccessAsync(Guid id, string? accessStamp, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(accessStamp))
        {
            return false;
        }

        var session = await _sessionsRepository.GetAsync(id, cancellationToken);

        return session is not null && accessStamp == SessionAccessStamp.For(session.PasswordHash);
    }
}
