using System.Diagnostics.Metrics;
using PartyKing.Domain.Models.Spotify;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Spotify.Metrics;
using PartyKing.Infrastructure.Spotify.RestApi;

namespace PartyKing.Api.Tests;

public class MeteredSpotifyIntegrationClientTests
{
    private sealed class FakeClient : ISpotifyIntegrationClient
    {
        public Exception? Failure { get; init; }

        public Uri GetAuthenticationUri(string state) => new("https://example.com/" + state);

        public Task<AuthenticationResult> AuthenticateAsync(string code, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReAuthenticationResult> ReAuthenticateAsync(string refreshToken, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Song>> SearchTracksAsync(string accessToken, string query, CancellationToken cancellationToken) =>
            Failure is null ? Task.FromResult<IReadOnlyList<Song>>([]) : throw Failure;

        public Task<Song?> GetTrackAsync(string accessToken, string trackId, CancellationToken cancellationToken) =>
            Task.FromResult<Song?>(null);
    }

    private static List<Dictionary<string, object?>> Capture(Func<MeteredSpotifyIntegrationClient, Task> act, ISpotifyIntegrationClient inner)
    {
        var tagSets = new List<Dictionary<string, object?>>();
        using var metrics = new SpotifyClientMetrics();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == SpotifyClientMetrics.DurationInstrumentName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
            tagSets.Add(tags.ToArray().ToDictionary(t => t.Key, t => t.Value)));
        listener.Start();

        try
        {
            act(new MeteredSpotifyIntegrationClient(inner, metrics)).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException)
        {
        }

        return tagSets;
    }

    [Fact]
    public void SearchTracksAsync_OnSuccess_RecordsTheOperationAndOutcome()
    {
        var tags = Assert.Single(Capture(c => c.SearchTracksAsync("t", "q", default), new FakeClient()));

        Assert.Equal("SearchTracksAsync", tags["operation"]);
        Assert.Equal(SpotifyClientMetrics.SuccessOutcome, tags["outcome"]);
    }

    [Fact]
    public void GetTrackAsync_WhenTheTrackIsMissing_RecordsASuccessOutcome()
    {
        var tags = Assert.Single(Capture(c => c.GetTrackAsync("t", "id", default), new FakeClient()));

        Assert.Equal("GetTrackAsync", tags["operation"]);
        Assert.Equal(SpotifyClientMetrics.SuccessOutcome, tags["outcome"]);
    }

    [Fact]
    public void SearchTracksAsync_WhenTheInnerCallThrows_RecordsAnExceptionOutcome()
    {
        var inner = new FakeClient { Failure = new InvalidOperationException("boom") };

        var tags = Assert.Single(Capture(c => c.SearchTracksAsync("t", "q", default), inner));

        Assert.Equal(SpotifyClientMetrics.ExceptionOutcome, tags["outcome"]);
    }

    [Fact]
    public void GetAuthenticationUri_RecordsNothing()
    {
        var tagSets = Capture(c => { c.GetAuthenticationUri("s"); return Task.CompletedTask; }, new FakeClient());

        Assert.Empty(tagSets);
    }
}
