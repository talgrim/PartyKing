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
    public void Records_granted_outcome_with_component()
    {
        var tags = Assert.Single(Capture(m => m.RecordDuration("RequireSessionAccess", AuthorizationMetrics.Start(), true)));

        Assert.Equal("RequireSessionAccess", tags["component"]);
        Assert.Equal(AuthorizationMetrics.GrantedOutcome, tags["outcome"]);
    }

    [Fact]
    public void Records_denied_outcome_for_failed_authentication()
    {
        var tags = Assert.Single(Capture(m => m.RecordDuration("CustomCookie", AuthorizationMetrics.Start(), false)));

        Assert.Equal("CustomCookie", tags["component"]);
        Assert.Equal(AuthorizationMetrics.DeniedOutcome, tags["outcome"]);
    }
}
