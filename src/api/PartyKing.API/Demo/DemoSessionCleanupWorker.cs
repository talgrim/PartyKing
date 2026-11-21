using PartyKing.API.Workers;
using PartyKing.Application.Demo.Services;

namespace PartyKing.API.Demo;

/// <summary>
/// Deletes the demo sessions that expired. Registered only when the demo is on. It sweeps at startup too, which ends
/// the sessions that expired while the server was stopped.
/// </summary>
public class DemoSessionCleanupWorker : PeriodicSweepWorker
{
    public DemoSessionCleanupWorker(IServiceScopeFactory scopeFactory, ILogger<DemoSessionCleanupWorker> logger)
        : base(scopeFactory, logger)
    {
    }

    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

    protected override string Name => "demo session sweep";

    protected override Task SweepAsync(IServiceProvider services, CancellationToken stoppingToken)
    {
        return services.GetRequiredService<IDemoSessionCleanup>().SweepAsync(stoppingToken);
    }
}
