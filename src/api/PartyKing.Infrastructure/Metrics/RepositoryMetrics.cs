using System.Diagnostics.Metrics;

namespace PartyKing.Infrastructure.Metrics;

/// <summary>Owns the meter that times repository calls; exported through OpenTelemetry.</summary>
public sealed class RepositoryMetrics : IDisposable
{
    public const string MeterName = "PartyKing.Infrastructure";
    public const string DurationInstrumentName = "partyking.repository.duration";
    public const string SuccessOutcome = "success";
    public const string ExceptionOutcome = "exception";

    private readonly Meter _meter;
    private readonly Histogram<double> _duration;

    public RepositoryMetrics()
    {
        _meter = new Meter(MeterName);
        _duration = _meter.CreateHistogram<double>(
            DurationInstrumentName,
            unit: "ms",
            description: "Duration of a repository call.");
    }

    public void RecordDuration(string repository, string operation, double durationMs, bool succeeded)
    {
        _duration.Record(
            durationMs,
            new KeyValuePair<string, object?>("repository", repository),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("outcome", succeeded ? SuccessOutcome : ExceptionOutcome));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
