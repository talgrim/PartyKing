using System.Diagnostics.Metrics;

namespace PartyKing.API.Metrics;

/// <summary>Owns the meter that times controller actions; exported through OpenTelemetry.</summary>
public sealed class ActionMetrics : IDisposable
{
    public const string MeterName = "PartyKing.API";
    public const string DurationInstrumentName = "partyking.controller.action.duration";

    private readonly Meter _meter;
    private readonly Histogram<double> _duration;

    public ActionMetrics()
    {
        _meter = new Meter(MeterName);
        _duration = _meter.CreateHistogram<double>(
            DurationInstrumentName,
            unit: "ms",
            description: "Execution time of a controller action.");
    }

    public void RecordDuration(
        double durationMs,
        string controller,
        string action,
        string method,
        int statusCode,
        string outcome)
    {
        _duration.Record(
            durationMs,
            new KeyValuePair<string, object?>("controller", controller),
            new KeyValuePair<string, object?>("action", action),
            new KeyValuePair<string, object?>("http.request.method", method),
            new KeyValuePair<string, object?>("http.response.status_code", statusCode),
            new KeyValuePair<string, object?>("outcome", outcome));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
