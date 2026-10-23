using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Player.Services;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Application.Spotify.Services;
using PartyKing.Application.System;
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
    Task<ErrorOr<SessionDto>> CreateAsync(string? name, string? password, CancellationToken cancellationToken);
    Task<ErrorOr<Success>> RenameAsync(Guid id, string? name, CancellationToken cancellationToken);
    Task<ErrorOr<Success>> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Removes the host so someone else can claim the session.</summary>
    Task<ErrorOr<Success>> ReleaseAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Replaces the password; everyone who had unlocked the session has to enter the new one.</summary>
    Task<ErrorOr<Success>> ChangePasswordAsync(Guid id, string? password, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the password. Returns the access stamp to remember, or Unauthorized, the same for a wrong password
    /// and an unknown session.
    /// </summary>
    Task<ErrorOr<string>> JoinAsync(Guid id, string? password, CancellationToken cancellationToken);

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

    /// <summary>True when <paramref name="accessStamp"/> was issued for the session's current password.</summary>
    Task<bool> HasAccessAsync(Guid id, string? accessStamp, CancellationToken cancellationToken);
}

internal class SessionService : ISessionService
{
    private const int MaxCodeAttempts = 10;
    private const int MaxNameLength = 60;
    private const int MinPasswordLength = 4;
    private const int MaxPasswordLength = 100;
    private const int MaxTokenLength = 1000;
    private static readonly TimeSpan JoinTokenLifetime = TimeSpan.FromMinutes(5);

    private readonly ISessionsRepository _sessionsRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPlayerStateStore _playerStateStore;
    private readonly IHostTokenProvider _hostTokenProvider;
    private readonly ISlideshowService _slideshowService;
    private readonly IJoinTokenProtector _joinTokenProtector;
    private readonly IDateTimeProvider _clock;

    // Verified against when the session is unknown, so both failures cost the same time
    private readonly string _dummyHash;

    public SessionService(
        ISessionsRepository sessionsRepository,
        IPasswordHasher passwordHasher,
        IPlayerStateStore playerStateStore,
        IHostTokenProvider hostTokenProvider,
        ISlideshowService slideshowService,
        IJoinTokenProtector joinTokenProtector,
        IDateTimeProvider clock)
    {
        _sessionsRepository = sessionsRepository;
        _passwordHasher = passwordHasher;
        _playerStateStore = playerStateStore;
        _hostTokenProvider = hostTokenProvider;
        _slideshowService = slideshowService;
        _joinTokenProtector = joinTokenProtector;
        _clock = clock;
        _dummyHash = passwordHasher.Hash(Guid.NewGuid().ToString());
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

    public async Task<ErrorOr<SessionDto>> CreateAsync(
        string? name,
        string? password,
        CancellationToken cancellationToken)
    {
        if (name is { Length: > MaxNameLength })
        {
            return NameTooLong();
        }

        if (ValidatePassword(password) is { } invalid)
        {
            return invalid;
        }

        var passwordHash = _passwordHasher.Hash(password!);

        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var session = new Session(SessionCodeGenerator.Generate(), name ?? string.Empty, passwordHash);

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
        var hostProfileId = (await _sessionsRepository.GetAsync(id, cancellationToken))?.HostProfileId;

        var deleted = await _sessionsRepository.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        ForgetHost(id, hostProfileId);

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
            return NotFound();
        }

        ForgetHost(id, hostProfileId);

        return Result.Success;
    }

    public async Task<ErrorOr<Success>> ChangePasswordAsync(
        Guid id,
        string? password,
        CancellationToken cancellationToken)
    {
        if (ValidatePassword(password) is { } invalid)
        {
            return invalid;
        }

        var updated = await _sessionsRepository.UpdatePasswordHashAsync(
            id,
            _passwordHasher.Hash(password!),
            cancellationToken);

        return updated ? Result.Success : NotFound();
    }

    public async Task<ErrorOr<string>> JoinAsync(Guid id, string? password, CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(id, cancellationToken);

        // An over-long password can't be right, and skipping the hash keeps it from being a cheap CPU burner
        var valid = password is { Length: <= MaxPasswordLength }
            && _passwordHasher.Verify(password, session?.PasswordHash ?? _dummyHash);

        return session is null || !valid
            ? Error.Unauthorized(description: "Wrong session password")
            : AccessStamp(session.PasswordHash);
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
            return NotFound();
        }

        var isHost = profileId is not null && session.HostProfileId == profileId;

        if (!isHost && !isAdmin)
        {
            return Error.Forbidden(description: "Only the host or an admin can create a join code");
        }

        var expiresAt = _clock.UtcNow.Add(JoinTokenLifetime);
        var token = _joinTokenProtector.Protect(new JoinTokenPayload(id, AccessStamp(session.PasswordHash)), expiresAt);

        return new JoinTokenDto(token, expiresAt);
    }

    public async Task<ErrorOr<string>> JoinWithTokenAsync(Guid id, string? token, CancellationToken cancellationToken)
    {
        var payload = token is { Length: > 0 and <= MaxTokenLength } ? _joinTokenProtector.Unprotect(token) : null;
        var session = await _sessionsRepository.GetAsync(id, cancellationToken);

        // The stamp ties the token to the current password, so changing the password revokes outstanding tokens
        return session is null
            || payload is null
            || payload.SessionId != id
            || payload.AccessStamp != AccessStamp(session.PasswordHash)
            ? Error.Unauthorized(description: "Invalid or expired join code")
            : AccessStamp(session.PasswordHash);
    }

    public async Task<bool> HasAccessAsync(Guid id, string? accessStamp, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(accessStamp))
        {
            return false;
        }

        var session = await _sessionsRepository.GetAsync(id, cancellationToken);

        return session is not null && accessStamp == AccessStamp(session.PasswordHash);
    }

    /// <summary>Drops what the server remembers about the session's player and its host's Spotify token.</summary>
    private void ForgetHost(Guid sessionId, string? hostProfileId)
    {
        _playerStateStore.Remove(sessionId);

        // The account may host another session by now, but then it just refreshes its token again
        if (hostProfileId is not null)
        {
            _hostTokenProvider.Invalidate(hostProfileId);
        }
    }

    /// <summary>Changes whenever the password does, which invalidates the unlocks issued for the old one.</summary>
    private static string AccessStamp(string passwordHash)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)))[..16];
    }

    private static Error? ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
        {
            return Error.Validation(description: $"The password must be at least {MinPasswordLength} characters");
        }

        return password.Length > MaxPasswordLength
            ? Error.Validation(description: $"The password must be at most {MaxPasswordLength} characters")
            : null;
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
