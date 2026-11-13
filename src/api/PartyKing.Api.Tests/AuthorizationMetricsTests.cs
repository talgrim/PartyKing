using System.Diagnostics.Metrics;
using PartyKing.API.Metrics;

namespace PartyKing.Api.Tests;

public class AuthorizationMetricsTests
{
    /// <summary>
    /// The meter is process-wide, so tests of other classes running in parallel record into the same instrument;
    /// only what carries <paramref name="component"/>, which each test makes up for itself, is the test's own.
    /// </summary>
    private static List<Dictionary<string, object?>> Capture(string component, Action<AuthorizationMetrics> act)
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
        {
            var tagSet = tags.ToArray().ToDictionary(t => t.Key, t => t.Value);
            if (Equals(tagSet["component"], component))
            {
                lock (tagSets)
                {
                    tagSets.Add(tagSet);
                }
            }
        });
        listener.Start();

        act(metrics);

        return tagSets;
    }

    [Fact]
    public void RecordDuration_WhenGranted_RecordsAGrantedOutcomeWithTheComponent()
    {
        const string component = "AuthorizationMetricsTests.Granted";

        var tags = Assert.Single(Capture(component, m => m.RecordDuration(component, 0, true)));

        Assert.Equal(component, tags["component"]);
        Assert.Equal(AuthorizationMetrics.GrantedOutcome, tags["outcome"]);
    }

    [Fact]
    public void RecordDuration_WhenDenied_RecordsADeniedOutcome()
    {
        const string component = "AuthorizationMetricsTests.Denied";

        var tags = Assert.Single(Capture(component, m => m.RecordDuration(component, 0, false)));

        Assert.Equal(component, tags["component"]);
        Assert.Equal(AuthorizationMetrics.DeniedOutcome, tags["outcome"]);
    }
}
