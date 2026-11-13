using System.Security.Cryptography;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Demo.Helpers;
using PartyKing.Application.Demo.Models;
using PartyKing.Application.Ports;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.System;
using PartyKing.Contract.V1.Demo;
using PartyKing.Domain.Entities.Demo;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Repositories;
using PartyKing.Domain.Results;

namespace PartyKing.Application.Demo.Services;

public interface IDemoSessionService
{
    DemoConfigDto GetConfig();

    /// <summary>
    /// Makes a session for a visitor, with a synthetic host the visitor becomes; the caller stores the result in cookies.
    /// Not found when the server is not a demo, a conflict while <see cref="DemoConfiguration.MaxSessions"/> exist.
    /// </summary>
    Task<ErrorOr<DemoStart>> StartAsync(CancellationToken cancellationToken);

    /// <summary>When the demo session ends; not found for a session that is not a demo one.</summary>
    Task<ErrorOr<DemoSessionDto>> GetAsync(Guid sessionId, CancellationToken cancellationToken);
}

internal class DemoSessionService : IDemoSessionService
{
    private const string SessionName = "Demo party";
    private const string HostName = "Demo host";
    private const string ProfileIdPrefix = "demo-";

    private readonly DemoConfiguration _configuration;
    private readonly ISessionAdminService _sessionAdminService;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly ISpotifyProfilesRepository _profilesRepository;
    private readonly IDemoSessionsRepository _demoSessionsRepository;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<DemoSessionService> _logger;

    public DemoSessionService(
        IOptions<DemoConfiguration> options,
        ISessionAdminService sessionAdminService,
        ISessionsRepository sessionsRepository,
        ISpotifyProfilesRepository profilesRepository,
        IDemoSessionsRepository demoSessionsRepository,
        IDateTimeProvider clock,
        ILogger<DemoSessionService> logger)
    {
        _configuration = options.Value;
        _sessionAdminService = sessionAdminService;
        _sessionsRepository = sessionsRepository;
        _profilesRepository = profilesRepository;
        _demoSessionsRepository = demoSessionsRepository;
        _clock = clock;
        _logger = logger;
    }

    public DemoConfigDto GetConfig()
    {
        return new DemoConfigDto(_configuration.Enabled, _clock.UtcNow);
    }

    public async Task<ErrorOr<DemoStart>> StartAsync(CancellationToken cancellationToken)
    {
        if (!_configuration.Enabled)
        {
            return DemoErrors.NotEnabled();
        }

        if (await _demoSessionsRepository.CountAsync(cancellationToken) >= _configuration.MaxSessions)
        {
            return DemoErrors.Full();
        }

        var now = _clock.UtcNow.UtcDateTime;
        var lifetime = TimeSpan.FromMinutes(_configuration.LifetimeMinutes);
        var profileId = $"{ProfileIdPrefix}{Guid.NewGuid():N}";

        // The refresh token is unique per profile; no Spotify call ever uses it
        await _profilesRepository.CreateProfileAsync(
            new AuthenticationResult(
                profileId,
                HostName,
                AccessToken: "demo",
                RefreshToken: $"demo-refresh-{Guid.NewGuid():N}",
                ExpiresIn: (int)lifetime.TotalSeconds),
            cancellationToken);

        Guid? sessionId = null;
        try
        {
            var created = await _sessionAdminService.CreateAsync(SessionName, NewPassword(), cancellationToken);
            if (created.IsError)
            {
                await AbandonAsync(null, profileId);
                return created.Errors;
            }

            sessionId = created.Value.Id;

            // Recorded before the claim, so a failure from here on still ends with the session being swept
            var demoSession = DemoSession.Start(sessionId.Value, profileId, now, lifetime);
            await _demoSessionsRepository.AddAsync(demoSession, cancellationToken);

            var claim = await _sessionsRepository.TryClaimAsync(sessionId.Value, profileId, cancellationToken);
            var session = claim == SessionClaimResult.Claimed
                ? await _sessionsRepository.GetAsync(sessionId.Value, cancellationToken)
                : null;

            if (session is null)
            {
                await AbandonAsync(sessionId, profileId);
                return DemoErrors.StartFailed();
            }

            return new DemoStart(
                session.Id,
                profileId,
                AccessToken: "demo",
                AccessTokenExpiresIn: (int)lifetime.TotalSeconds,
                session.AccessStamp,
                ToUtc(demoSession.ExpiresAt));
        }
        catch
        {
            await AbandonAsync(sessionId, profileId);
            throw;
        }
    }

    public async Task<ErrorOr<DemoSessionDto>> GetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!_configuration.Enabled)
        {
            return DemoErrors.NotEnabled();
        }

        var demoSession = await _demoSessionsRepository.GetAsync(sessionId, cancellationToken);

        return demoSession is null
            ? DemoErrors.NotEnabled()
            : new DemoSessionDto(ToUtc(demoSession.ExpiresAt), _clock.UtcNow);
    }

    // Nobody types it: the visitor is let in with the cookie, guests with a QR code
    private static string NewPassword()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    }

    private static DateTimeOffset ToUtc(DateTime utc)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    }

    /// <summary>Takes back what a failed start made. It must not hide the failure it is called for.</summary>
    private async Task AbandonAsync(Guid? sessionId, string profileId)
    {
        try
        {
            if (sessionId is not null)
            {
                await _sessionAdminService.DeleteAsync(sessionId.Value, CancellationToken.None);
            }

            await _profilesRepository.DeleteProfileAsync(profileId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not clean up a demo session that failed to start ({SessionId})", sessionId);
        }
    }
}
