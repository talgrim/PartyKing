using System.Diagnostics.Metrics;
using PartyKing.API.Metrics;

namespace PartyKing.Api.Tests;

public class AuthorizationMetricsTests
{
    private static List<Dictionary<string, object?>> Capture(Action<AuthorizationMetrics> act)
    {
        var tagSets = new List<Dictionary<string, object?>>();
        using var metrics = new AuthorizationMetrics();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == AuthorizationMetrics.DurationInstrumentName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
            tagSets.Add(tags.ToArray().ToDictionary(t => t.Key, t => t.Value)));
        listener.Start();

        act(metrics);

        return tagSets;
    }

    [Fact]
    public void RecordDuration_WhenGranted_RecordsAGrantedOutcomeWithTheComponent()
    {
        var tags = Assert.Single(Capture(m => m.RecordDuration("RequireSessionAccess", 0, true)));

        Assert.Equal("RequireSessionAccess", tags["component"]);
        Assert.Equal(AuthorizationMetrics.GrantedOutcome, tags["outcome"]);
    }

    [Fact]
    public void RecordDuration_WhenDenied_RecordsADeniedOutcome()
    {
        var tags = Assert.Single(Capture(m => m.RecordDuration("CustomCookie", 0, false)));

        Assert.Equal("CustomCookie", tags["component"]);
        Assert.Equal(AuthorizationMetrics.DeniedOutcome, tags["outcome"]);
    }
}
