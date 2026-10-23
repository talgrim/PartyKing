using System.Diagnostics;

namespace PartyKing.Infrastructure.Metrics;

/// <summary>Base of the repository decorators: times a call and records it under the repository's name.</summary>
internal abstract class MeteredRepository
{
    private readonly string _repositoryName;
    private readonly RepositoryMetrics _metrics;

    protected MeteredRepository(string repositoryName, RepositoryMetrics metrics)
    {
        _repositoryName = repositoryName;
        _metrics = metrics;
    }

    protected async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> call)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var succeeded = false;

        try
        {
            var result = await call();
            succeeded = true;

            return result;
        }
        finally
        {
            var durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            _metrics.RecordDuration(_repositoryName, operation, durationMs, succeeded);
        }
    }

    protected Task MeasureAsync(string operation, Func<Task> call)
    {
        return MeasureAsync(operation, async () =>
        {
            await call();
            return true;
        });
    }
}
