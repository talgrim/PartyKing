using PartyKing.Application.Ports;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Results;
using SpotifyAPI.Web;

namespace PartyKing.Infrastructure.Spotify.RestApi;

/// <summary>Turns the Spotify SDK's <see cref="APIException"/> into <see cref="SpotifyApiException"/>, so callers never see the SDK.</summary>
internal sealed class SpotifyApiExceptionClient : ISpotifyIntegrationClient
{
    private readonly ISpotifyIntegrationClient _inner;

    public SpotifyApiExceptionClient(ISpotifyIntegrationClient inner)
    {
        _inner = inner;
    }

    public Uri GetAuthenticationUri(string state)
    {
        return _inner.GetAuthenticationUri(state);
    }

    public Task<AuthenticationResult> AuthenticateAsync(string code, CancellationToken cancellationToken)
    {
        return TranslateAsync(() => _inner.AuthenticateAsync(code, cancellationToken));
    }

    public Task<ReAuthenticationResult> ReAuthenticateAsync(string refreshToken, CancellationToken cancellationToken)
    {
        return TranslateAsync(() => _inner.ReAuthenticateAsync(refreshToken, cancellationToken));
    }

    public Task<IReadOnlyList<Song>> SearchTracksAsync(
        string accessToken,
        string query,
        CancellationToken cancellationToken)
    {
        return TranslateAsync(() => _inner.SearchTracksAsync(accessToken, query, cancellationToken));
    }

    public Task<Song?> GetTrackAsync(string accessToken, string trackId, CancellationToken cancellationToken)
    {
        return TranslateAsync(() => _inner.GetTrackAsync(accessToken, trackId, cancellationToken));
    }

    public Task<Playlist?> GetPlaylistAsync(
        string accessToken,
        string playlistId,
        CancellationToken cancellationToken)
    {
        return TranslateAsync(() => _inner.GetPlaylistAsync(accessToken, playlistId, cancellationToken));
    }

    public Task<IReadOnlyList<Playlist>> GetCurrentUserPlaylistsAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        return TranslateAsync(() => _inner.GetCurrentUserPlaylistsAsync(accessToken, cancellationToken));
    }

    public Task<IReadOnlyList<Song>> GetPlaylistTracksAsync(
        string accessToken,
        string playlistId,
        CancellationToken cancellationToken)
    {
        return TranslateAsync(() => _inner.GetPlaylistTracksAsync(accessToken, playlistId, cancellationToken));
    }

    private static async Task<T> TranslateAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (APIException exception)
        {
            throw new SpotifyApiException(exception.Response?.StatusCode, exception.Message, exception);
        }
    }
}
