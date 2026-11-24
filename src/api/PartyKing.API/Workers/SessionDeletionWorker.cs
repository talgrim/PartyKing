using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Sessions.Services;

namespace PartyKing.API.Workers;

/// <summary>
/// Deletes the sessions whose deletion a guest asked for once their delay is over. Registered on every server, the
/// demo included (where no deletion is ever scheduled and the sweep finds nothing). It sweeps at startup too, which
/// deletes what became due while the server was stopped.
/// </summary>
public class SessionDeletionWorker : PeriodicSweepWorker
{
    private readonly TimeSpan _interval;

    public SessionDeletionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<SessionDeletionConfiguration> configuration,
        ILogger<SessionDeletionWorker> logger)
        : base(scopeFactory, logger)
    {
        _interval = configuration.Value.SweepInterval;
    }

    protected override TimeSpan Interval => _interval;

    protected override string Name => "scheduled session deletion sweep";

    protected override Task SweepAsync(IServiceProvider services, CancellationToken stoppingToken)
    {
        return services.GetRequiredService<IScheduledSessionDeletionSweep>().SweepAsync(stoppingToken);
    }
}
