using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Domain.Models.Spotify;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Spotify.Mappings;
using SpotifyAPI.Web;

namespace PartyKing.Infrastructure.Spotify.RestApi;

public interface ISpotifyIntegrationClient
{
    Uri GetAuthenticationUri(string state);
    Task<AuthenticationResult> AuthenticateAsync(string code, CancellationToken cancellationToken);
    Task<ReAuthenticationResult> ReAuthenticateAsync(string refreshToken, CancellationToken cancellationToken);

    Task<IReadOnlyList<Song>> SearchTracksAsync(string accessToken, string query, CancellationToken cancellationToken);
    Task<Song?> GetTrackAsync(string accessToken, string trackId, CancellationToken cancellationToken);
}

internal class SpotifyIntegrationClient : ISpotifyIntegrationClient
{
    private readonly ISpotifyClientFactory _clientFactory;
    private readonly SpotifyConfiguration _config;
    private readonly ILogger<ISpotifyIntegrationClient> _logger;

    private ISpotifyClient? _client;

    // Playback in the browser (Web Playback SDK) needs streaming plus the account's email and subscription;
    // reading the playback state lets the host's player take a song over from another of the host's devices
    private static readonly ICollection<string> Scope =
    [
        Scopes.Streaming,
        Scopes.UserReadEmail,
        Scopes.UserReadPrivate,
        Scopes.UserModifyPlaybackState,
        Scopes.UserReadPlaybackState
    ];

    public SpotifyIntegrationClient(
        ISpotifyClientFactory clientFactory,
        IOptions<SpotifyConfiguration> config,
        ILogger<ISpotifyIntegrationClient> logger)
    {
        _clientFactory = clientFactory;
        _config = config.Value;
        _logger = logger;
    }

    public Uri GetAuthenticationUri(string state)
    {
        var loginRequest = new LoginRequest(_config.RedirectUri, _config.ClientId, LoginRequest.ResponseType.Code)
        {
            Scope = Scope,
            State = state,
        };

        return loginRequest.ToUri();
    }

    public async Task<AuthenticationResult> AuthenticateAsync(string code, CancellationToken cancellationToken)
    {
        var oauth = _clientFactory.CreateOAuthClient();
        var request =
            new AuthorizationCodeTokenRequest(_config.ClientId, _config.ClientSecret, code, _config.RedirectUri);

        var response = await oauth.RequestToken(request, cancellationToken);

        var profile = await GetProfileAsync(response.AccessToken, cancellationToken);

        return new AuthenticationResult(
            profile.Id,
            string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.Id : profile.DisplayName,
            response.AccessToken,
            response.RefreshToken,
            response.ExpiresIn);
    }

    public async Task<ReAuthenticationResult> ReAuthenticateAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var oauth = _clientFactory.CreateOAuthClient();
        var request =
            new AuthorizationCodeRefreshRequest(_config.ClientId, _config.ClientSecret, refreshToken);

        var response = await oauth.RequestToken(request, cancellationToken);

        return new ReAuthenticationResult(response.AccessToken, response.RefreshToken, response.ExpiresIn);
    }

    public async Task<IReadOnlyList<Song>> SearchTracksAsync(
        string accessToken,
        string query,
        CancellationToken cancellationToken)
    {
        var client = _clientFactory.CreateClient(accessToken);

        var response = await client.Search.Item(new SearchRequest(SearchRequest.Types.Track, query), cancellationToken);

        return response.Tracks.Items?.Select(x => x.ToDomain()).ToList() ?? [];
    }

    public async Task<Song?> GetTrackAsync(string accessToken, string trackId, CancellationToken cancellationToken)
    {
        var client = _clientFactory.CreateClient(accessToken);

        try
        {
            var track = await client.Tracks.Get(trackId, cancellationToken);
            return track.ToDomain();
        }
        catch (APIException exception) when (exception.Response?.StatusCode is System.Net.HttpStatusCode.NotFound
                                                 or System.Net.HttpStatusCode.BadRequest)
        {
            return null;
        }
    }

    private async Task<PrivateUser> GetProfileAsync(string accessToken, CancellationToken cancellationToken)
    {
        InitializeClientIfNecessary(accessToken);

        return await _client!.UserProfile.Current(cancellationToken);
    }

    private void InitializeClientIfNecessary(string accessToken)
    {
        _client ??= _clientFactory.CreateClient(accessToken);
    }
}
