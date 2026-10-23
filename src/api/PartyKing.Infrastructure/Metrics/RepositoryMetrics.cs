using System.Diagnostics.Metrics;

namespace PartyKing.Infrastructure.Metrics;

/// <summary>Owns the meter that times repository calls; exported through OpenTelemetry.</summary>
public sealed class RepositoryMetrics : IDisposable
{
    public const string MeterName = "PartyKing.Infrastructure";
    public const string DurationInstrumentName = "partyking.repository.duration";
    public const string SuccessOutcome = "success";
    public const string ExceptionOutcome = "exception";
    public const string AdvanceLockWaitInstrumentName = "partyking.queue.advance_lock.wait";

    private readonly Meter _meter;
    private readonly Histogram<double> _duration;
    private readonly Histogram<double> _advanceLockWait;

    public RepositoryMetrics()
    {
        _meter = new Meter(MeterName);
        _duration = _meter.CreateHistogram<double>(
            DurationInstrumentName,
            unit: "ms",
            description: "Duration of a repository call.");
        _advanceLockWait = _meter.CreateHistogram<double>(
            AdvanceLockWaitInstrumentName,
            unit: "ms",
            description: "Time spent waiting for the queue's advance lock, which AdvanceAsync and RemoveAsync share.");
    }

    public void RecordDuration(string repository, string operation, double durationMs, bool succeeded)
    {
        _duration.Record(
            durationMs,
            new KeyValuePair<string, object?>("repository", repository),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("outcome", succeeded ? SuccessOutcome : ExceptionOutcome));
    }

    /// <param name="acquired">False when the wait ended without the lock, because the request was cancelled.</param>
    public void RecordAdvanceLockWait(string operation, double waitMs, bool acquired)
    {
        _advanceLockWait.Record(
            waitMs,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("acquired", acquired));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
