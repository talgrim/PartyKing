using ErrorOr;
using Microsoft.Extensions.Logging;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Sessions;
using PartyKing.Infrastructure.Repositories;

namespace PartyKing.Application.Sessions.Services;

public interface ISessionJoinTokenService
{
    /// <summary>
    /// Issues a short-lived token that unlocks the session without its password, for a QR code. Only the host
    /// (<paramref name="profileId"/>) or an admin may ask for one; anyone else gets Forbidden.
    /// </summary>
    Task<ErrorOr<JoinTokenDto>> CreateJoinTokenAsync(
        Guid id,
        string? profileId,
        bool isAdmin,
        CancellationToken cancellationToken);

    /// <summary>
    /// Redeems a join token. Returns the access stamp to remember, or Unauthorized, the same for a forged,
    /// expired or outdated token (the password changed) and for an unknown session.
    /// </summary>
    Task<ErrorOr<string>> JoinWithTokenAsync(Guid id, string? token, CancellationToken cancellationToken);
}

internal class SessionJoinTokenService : ISessionJoinTokenService
{
    private const int MaxTokenLength = 1000;
    private static readonly TimeSpan JoinTokenLifetime = TimeSpan.FromMinutes(5);

    private readonly ISessionsRepository _sessionsRepository;
    private readonly IJoinTokenProtector _joinTokenProtector;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<SessionJoinTokenService> _logger;

    public SessionJoinTokenService(
        ISessionsRepository sessionsRepository,
        IJoinTokenProtector joinTokenProtector,
        IDateTimeProvider clock,
        ILogger<SessionJoinTokenService> logger)
    {
        _sessionsRepository = sessionsRepository;
        _joinTokenProtector = joinTokenProtector;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ErrorOr<JoinTokenDto>> CreateJoinTokenAsync(
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

        if (!SessionRoles.IsManager(session, profileId, isAdmin))
        {
            return Error.Forbidden(description: "Only the host or an admin can create a join code");
        }

        var expiresAt = _clock.UtcNow.Add(JoinTokenLifetime);
        var payload = new JoinTokenPayload(id, SessionAccessStamp.For(session.PasswordHash));

        return new JoinTokenDto(_joinTokenProtector.Protect(payload, expiresAt), expiresAt);
    }

    public async Task<ErrorOr<string>> JoinWithTokenAsync(Guid id, string? token, CancellationToken cancellationToken)
    {
        var payload = token is { Length: > 0 and <= MaxTokenLength } ? _joinTokenProtector.Unprotect(token) : null;
        var session = await _sessionsRepository.GetAsync(id, cancellationToken);

        // The stamp ties the token to the current password, so changing the password revokes outstanding tokens
        if (session is null
            || payload is null
            || payload.SessionId != id
            || payload.AccessStamp != SessionAccessStamp.For(session.PasswordHash))
        {
            _logger.LogWarning("Failed to join session {SessionId}: invalid or expired join code", id);
            return Error.Unauthorized(description: "Invalid or expired join code");
        }

        _logger.LogDebug("Joined session {SessionId} with a join code", id);
        return SessionAccessStamp.For(session.PasswordHash);
    }
}
