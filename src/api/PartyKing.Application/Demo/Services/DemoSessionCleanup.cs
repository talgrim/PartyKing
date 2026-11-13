using Microsoft.Extensions.Logging;
using PartyKing.Application.Ports;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.System;
using PartyKing.Domain.Repositories;

namespace PartyKing.Application.Demo.Services;

public interface IDemoSessionCleanup
{
    /// <summary>
    /// Deletes the demo sessions that are over (with their photos, state and synthetic host) and says how many it deleted.
    /// A session that cannot be deleted is logged and tried again by the next sweep.
    /// </summary>
    Task<int> SweepAsync(CancellationToken cancellationToken);
}

internal class DemoSessionCleanup : IDemoSessionCleanup
{
    private readonly IDemoSessionsRepository _demoSessionsRepository;
    private readonly ISessionAdminService _sessionAdminService;
    private readonly ISpotifyProfilesRepository _profilesRepository;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<DemoSessionCleanup> _logger;

    public DemoSessionCleanup(
        IDemoSessionsRepository demoSessionsRepository,
        ISessionAdminService sessionAdminService,
        ISpotifyProfilesRepository profilesRepository,
        IDateTimeProvider clock,
        ILogger<DemoSessionCleanup> logger)
    {
        _demoSessionsRepository = demoSessionsRepository;
        _sessionAdminService = sessionAdminService;
        _profilesRepository = profilesRepository;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        var expired = await _demoSessionsRepository.ListExpiredAsync(_clock.UtcNow.UtcDateTime, cancellationToken);
        var deleted = 0;

        foreach (var demoSession in expired)
        {
            try
            {
                // The session first: its events carry the host, and the profile may only go once nothing hosts with it
                await _sessionAdminService.DeleteAsync(demoSession.SessionId, cancellationToken);
                await _profilesRepository.DeleteProfileAsync(demoSession.HostProfileId, cancellationToken);
                deleted++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Could not delete the expired demo session {SessionId}", demoSession.SessionId);
            }
        }

        if (deleted > 0)
        {
            _logger.LogInformation("Deleted {Count} expired demo sessions", deleted);
        }

        return deleted;
    }
}
