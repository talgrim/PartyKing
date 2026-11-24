using ErrorOr;
using Microsoft.Extensions.Logging;
using PartyKing.Application.System;
using PartyKing.Domain.Repositories;

namespace PartyKing.Application.Sessions.Services;

public interface IScheduledSessionDeletionSweep
{
    /// <summary>
    /// Deletes the sessions whose scheduled deletion is due (with their photos and state, as any delete) and says how
    /// many it deleted. A session that cannot be deleted is logged and tried again by the next sweep; one that is
    /// already gone (the host deleted it meanwhile) counts as done.
    /// </summary>
    Task<int> SweepAsync(CancellationToken cancellationToken);
}

internal class ScheduledSessionDeletionSweep : IScheduledSessionDeletionSweep
{
    private readonly ISessionsRepository _sessionsRepository;
    private readonly ISessionAdminService _sessionAdminService;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ScheduledSessionDeletionSweep> _logger;

    public ScheduledSessionDeletionSweep(
        ISessionsRepository sessionsRepository,
        ISessionAdminService sessionAdminService,
        IDateTimeProvider clock,
        ILogger<ScheduledSessionDeletionSweep> logger)
    {
        _sessionsRepository = sessionsRepository;
        _sessionAdminService = sessionAdminService;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        var due = await _sessionsRepository.ListDueForDeletionAsync(_clock.UtcNow.UtcDateTime, cancellationToken);
        var deleted = 0;

        foreach (var sessionId in due)
        {
            try
            {
                var result = await _sessionAdminService.DeleteAsync(sessionId, cancellationToken);

                if (!result.IsError)
                {
                    deleted++;
                }
                else if (result.FirstError.Type != ErrorType.NotFound)
                {
                    _logger.LogError(
                        "Could not delete session {SessionId} whose deletion was scheduled: {Error}",
                        sessionId,
                        result.FirstError.Description);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Could not delete session {SessionId} whose deletion was scheduled", sessionId);
            }
        }

        if (deleted > 0)
        {
            _logger.LogInformation("Deleted {Count} sessions whose scheduled deletion was due", deleted);
        }

        return deleted;
    }
}
