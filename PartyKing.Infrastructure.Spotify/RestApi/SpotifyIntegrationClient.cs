using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Spotify.Mappings;
using SpotifyAPI.Web;

namespace PartyKing.Infrastructure.Spotify.RestApi;

public interface ISpotifyIntegrationClient
{
    Uri GetAuthenticationUri();
    Task<AuthenticationResult> AuthenticateAsync(string code, CancellationToken cancellationToken);
    Task<ReAuthenticationResult> ReAuthenticateAsync(string refreshToken, CancellationToken cancellationToken);

    Task<string> GetCurrentlyPlayingAsync(string accessToken, CancellationToken cancellationToken);
}

public class SpotifyIntegrationClient : ISpotifyIntegrationClient
{
    private readonly ISpotifyClientFactory _clientFactory;
    private readonly SpotifyConfiguration _config;
    private readonly ILogger<ISpotifyIntegrationClient> _logger;

    private ISpotifyClient? _client;

    private static readonly ICollection<string> Scope =
    [
        Scopes.UgcImageUpload,
        Scopes.UserReadPlaybackState,
        Scopes.UserModifyPlaybackState,
        Scopes.UserReadCurrentlyPlaying,
        Scopes.Streaming,
        Scopes.AppRemoteControl,
        Scopes.UserReadEmail,
        Scopes.UserReadPrivate,
        Scopes.PlaylistReadCollaborative,
        Scopes.PlaylistModifyPublic,
        Scopes.PlaylistReadPrivate,
        Scopes.PlaylistModifyPrivate,
        Scopes.UserLibraryModify,
        Scopes.UserLibraryRead,
        Scopes.UserTopRead,
        Scopes.UserReadPlaybackPosition,
        Scopes.UserReadRecentlyPlayed,
        Scopes.UserFollowRead,
        Scopes.UserFollowModify
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

    public Uri GetAuthenticationUri()
    {
        var loginRequest = new LoginRequest(_config.RedirectUri, _config.ClientId, LoginRequest.ResponseType.Code)
        {
            Scope = Scope,
#if DEBUG
            ShowDialog = true
#endif
        };

        return loginRequest.ToUri();
    }

    public async Task<AuthenticationResult> AuthenticateAsync(string code, CancellationToken cancellationToken)
    {
        var oauth = new OAuthClient();
        var request =
            new AuthorizationCodeTokenRequest(_config.ClientId, _config.ClientSecret, code, _config.RedirectUri);

        var response = await oauth.RequestToken(request, cancellationToken);

        var profileId = await GetProfileIdAsync(response.AccessToken, cancellationToken);

        return new AuthenticationResult(profileId, response.AccessToken, response.RefreshToken, response.ExpiresIn);
    }

    public async Task<ReAuthenticationResult> ReAuthenticateAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var oauth = new OAuthClient();
        var request =
            new AuthorizationCodeRefreshRequest(_config.ClientId, _config.ClientSecret, refreshToken);

        var response = await oauth.RequestToken(request, cancellationToken);

        return new ReAuthenticationResult(response.AccessToken, response.RefreshToken, response.ExpiresIn);
    }

    public async Task<string> GetCurrentlyPlayingAsync(string accessToken, CancellationToken cancellationToken)
    {
        InitializeClientIfNecessary(accessToken);

        var currentlyPlaying =
            await _client!.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest(), cancellationToken);

        return FormatCurrentSong(currentlyPlaying);
    }

    private async Task<string> GetProfileIdAsync(string accessToken, CancellationToken cancellationToken)
    {
        InitializeClientIfNecessary(accessToken);

        var profile = await _client!.UserProfile.Current(cancellationToken);
        return profile.Id;
    }

    private void InitializeClientIfNecessary(string accessToken)
    {
        _client ??= _clientFactory.CreateClient(accessToken);
    }

    private static string FormatCurrentSong(CurrentlyPlaying? currentlyPlaying)
    {
        if (currentlyPlaying?.Item is not FullTrack track)
            return string.Empty;

        var isPlaying = currentlyPlaying.IsPlaying ? "Playing" : "Stopped";
        var song = track.ToDomain();

        return $"{song.ToDisplayString()} ({isPlaying})";
    }
}