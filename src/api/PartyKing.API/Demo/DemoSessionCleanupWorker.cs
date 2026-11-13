using PartyKing.Application.Demo.Services;

namespace PartyKing.API.Demo;

/// <summary>
/// Deletes the demo sessions that expired. Registered only when the demo is on. It sweeps at startup too, which ends
/// the sessions that expired while the server was stopped.
/// </summary>
public class DemoSessionCleanupWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DemoSessionCleanupWorker> _logger;

    public DemoSessionCleanupWorker(IServiceScopeFactory scopeFactory, ILogger<DemoSessionCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            await SweepAsync(stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task SweepAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IDemoSessionCleanup>().SweepAsync(stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The next tick tries again; a failed sweep must not stop the worker
            _logger.LogError(exception, "The demo session sweep failed");
        }
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
