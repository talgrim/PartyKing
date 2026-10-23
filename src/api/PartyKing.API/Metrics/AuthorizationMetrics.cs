using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PartyKing.API.Metrics;

/// <summary>Times the custom authentication handlers and the session access filter; exported through OpenTelemetry.</summary>
public sealed class AuthorizationMetrics : IDisposable
{
    public const string DurationInstrumentName = "partyking.authorization.duration";
    public const string GrantedOutcome = "granted";
    public const string DeniedOutcome = "denied";

    private readonly Meter _meter;
    private readonly Histogram<double> _duration;

    public AuthorizationMetrics()
    {
        _meter = new Meter(ActionMetrics.MeterName);
        _duration = _meter.CreateHistogram<double>(
            DurationInstrumentName,
            unit: "ms",
            description: "Time spent authenticating or authorizing a request.");
    }

    public static long Start()
    {
        return Stopwatch.GetTimestamp();
    }

    /// <param name="component">What was measured: <c>RequireSessionAccess</c> or the authentication scheme name.</param>
    public void RecordDuration(string component, long startTimestamp, bool granted)
    {
        _duration.Record(
            Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
            new KeyValuePair<string, object?>("component", component),
            new KeyValuePair<string, object?>("outcome", granted ? GrantedOutcome : DeniedOutcome));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
