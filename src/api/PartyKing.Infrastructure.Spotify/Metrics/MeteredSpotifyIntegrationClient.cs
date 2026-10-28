using System.Diagnostics;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Spotify.RestApi;

namespace PartyKing.Infrastructure.Spotify.Metrics;

/// <summary>Times every call that reaches the Spotify Web API and delegates the work to the wrapped client.</summary>
public sealed class MeteredSpotifyIntegrationClient : ISpotifyIntegrationClient
{
    private readonly ISpotifyIntegrationClient _inner;
    private readonly SpotifyClientMetrics _metrics;

    public MeteredSpotifyIntegrationClient(ISpotifyIntegrationClient inner, SpotifyClientMetrics metrics)
    {
        _inner = inner;
        _metrics = metrics;
    }

    // Builds a URL locally, no request is made
    public Uri GetAuthenticationUri(string state)
    {
        return _inner.GetAuthenticationUri(state);
    }

    public Task<AuthenticationResult> AuthenticateAsync(string code, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(AuthenticateAsync), () => _inner.AuthenticateAsync(code, cancellationToken));
    }

    public Task<ReAuthenticationResult> ReAuthenticateAsync(string refreshToken, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(ReAuthenticateAsync), () => _inner.ReAuthenticateAsync(refreshToken, cancellationToken));
    }

    public Task<IReadOnlyList<Song>> SearchTracksAsync(
        string accessToken,
        string query,
        CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(SearchTracksAsync), () => _inner.SearchTracksAsync(accessToken, query, cancellationToken));
    }

    public Task<Song?> GetTrackAsync(string accessToken, string trackId, CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetTrackAsync), () => _inner.GetTrackAsync(accessToken, trackId, cancellationToken));
    }

    public Task<Playlist?> GetPlaylistAsync(
        string accessToken,
        string playlistId,
        CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetPlaylistAsync), () => _inner.GetPlaylistAsync(accessToken, playlistId, cancellationToken));
    }

    public Task<IReadOnlyList<Playlist>> GetCurrentUserPlaylistsAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetCurrentUserPlaylistsAsync), () => _inner.GetCurrentUserPlaylistsAsync(accessToken, cancellationToken));
    }

    public Task<IReadOnlyList<Song>> GetPlaylistTracksAsync(
        string accessToken,
        string playlistId,
        CancellationToken cancellationToken)
    {
        return MeasureAsync(nameof(GetPlaylistTracksAsync), () => _inner.GetPlaylistTracksAsync(accessToken, playlistId, cancellationToken));
    }

    private async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> call)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var succeeded = false;

        try
        {
            var result = await call();
            succeeded = true;

            return result;
        }
        finally
        {
            var durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            _metrics.RecordDuration(operation, durationMs, succeeded);
        }
    }
}
