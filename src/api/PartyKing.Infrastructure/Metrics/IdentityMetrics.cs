using System.Diagnostics.Metrics;

namespace PartyKing.Infrastructure.Metrics;

/// <summary>Owns the meter of the identity client: how often an answer came from the cache, and how the calls went.</summary>
public sealed class IdentityMetrics : IDisposable
{
    public const string MeterName = "PartyKing.Infrastructure.Identity";
    public const string CacheHitsInstrumentName = "partyking.identity.cache.hits";
    public const string CallsInstrumentName = "partyking.identity.calls";
    public const string CallDurationInstrumentName = "partyking.identity.call.duration";
    public const string StaleAnswersInstrumentName = "partyking.identity.stale";

    public const string AccountOutcome = "account";
    public const string AnonymousOutcome = "anonymous";
    public const string ForbiddenOutcome = "forbidden";
    public const string UnavailableOutcome = "unavailable";

    private readonly Meter _meter;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _calls;
    private readonly Histogram<double> _callDuration;
    private readonly Counter<long> _staleAnswers;

    public IdentityMetrics()
    {
        _meter = new Meter(MeterName);
        _cacheHits = _meter.CreateCounter<long>(
            CacheHitsInstrumentName,
            description: "Answers about a session cookie that came from the cache.");
        _calls = _meter.CreateCounter<long>(
            CallsInstrumentName,
            description: "Calls to the identity service, by outcome.");
        _callDuration = _meter.CreateHistogram<double>(
            CallDurationInstrumentName,
            unit: "ms",
            description: "Duration of a call to the identity service.");
        _staleAnswers = _meter.CreateCounter<long>(
            StaleAnswersInstrumentName,
            description: "Answers taken from an expired cache entry because the identity service was unreachable.");
    }

    public void RecordCacheHit()
    {
        _cacheHits.Add(1);
    }

    public void RecordCall(string outcome, double durationMs)
    {
        var tag = new KeyValuePair<string, object?>("outcome", outcome);

        _calls.Add(1, tag);
        _callDuration.Record(durationMs, tag);
    }

    public void RecordStaleAnswer()
    {
        _staleAnswers.Add(1);
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
