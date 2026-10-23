using System.Diagnostics.Metrics;

namespace PartyKing.Infrastructure.Spotify.Metrics;

/// <summary>Owns the meter that times calls to the Spotify Web API; exported through OpenTelemetry.</summary>
public sealed class SpotifyClientMetrics : IDisposable
{
    public const string MeterName = "PartyKing.Infrastructure.Spotify";
    public const string DurationInstrumentName = "partyking.spotify.client.duration";
    public const string SuccessOutcome = "success";
    public const string ExceptionOutcome = "exception";

    private readonly Meter _meter;
    private readonly Histogram<double> _duration;

    public SpotifyClientMetrics()
    {
        _meter = new Meter(MeterName);
        _duration = _meter.CreateHistogram<double>(
            DurationInstrumentName,
            unit: "ms",
            description: "Duration of a call made by the Spotify integration client.");
    }

    public void RecordDuration(string operation, double durationMs, bool succeeded)
    {
        _duration.Record(
            durationMs,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("outcome", succeeded ? SuccessOutcome : ExceptionOutcome));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
