namespace PartyKing.API.Workers;

/// <summary>
/// A background service that runs one sweep at startup and then on every tick of a timer, each in a scope of its own.
/// A sweep that throws is logged and tried again on the next tick; it never stops the worker. The startup sweep deals
/// with what became due while the server was stopped.
/// </summary>
public abstract class PeriodicSweepWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    protected PeriodicSweepWorker(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>The time between two sweeps. Read once, when the worker starts.</summary>
    protected abstract TimeSpan Interval { get; }

    /// <summary>What the sweep is called in the log line of a failure.</summary>
    protected abstract string Name { get; }

    /// <summary>Runs one sweep with the services of the scope it was given.</summary>
    protected abstract Task SweepAsync(IServiceProvider services, CancellationToken stoppingToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            await TrySweepAsync(stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task TrySweepAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await SweepAsync(scope.ServiceProvider, stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The next tick tries again; a failed sweep must not stop the worker
            _logger.LogError(exception, "The {Sweep} failed", Name);
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
