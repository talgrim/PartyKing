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

    // Verified against when the session is unknown, so both failures cost the same time
    private readonly string _dummyHash;

    public SessionAccessService(
        ISessionsRepository sessionsRepository,
        IPasswordHasher passwordHasher,
        ISessionAuthorizer authorizer,
        ILogger<SessionAccessService> logger)
    {
        _sessionsRepository = sessionsRepository;
        _passwordHasher = passwordHasher;
        _authorizer = authorizer;
        _logger = logger;
        _dummyHash = passwordHasher.Hash(Guid.NewGuid().ToString(), PasswordHasher.SessionIterations);
    }

    public async Task<ErrorOr<string>> JoinAsync(Guid id, string? password, CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(id, cancellationToken);

        // An over-long password can't be right, and skipping the hash keeps it from being a cheap CPU burner
        var valid = password is { Length: <= SessionPasswordPolicy.MaxLength }
            && _passwordHasher.Verify(password, session?.PasswordHash ?? _dummyHash);

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
