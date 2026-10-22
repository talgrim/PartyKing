using ErrorOr;
using Microsoft.AspNetCore.Http;
using OneOf.Types;
using PartyKing.Contract.V1.Spotify;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Authorization;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Spotify.RestApi;

namespace PartyKing.Application.Spotify.Services;

public interface ISpotifyService
{
    Uri GetAuthenticationUrl();
    Task<ErrorOr<Success<UserDto>>> AuthenticateByCodeAsync(string code, CancellationToken cancellationToken);
    Task<ErrorOr<string>> AuthenticateByProfileIdAsync(int id, CancellationToken cancellationToken);
    Task<string> GetCurrentlyPlayingAsync(string accessToken, CancellationToken cancellationToken);
}

public class SpotifyService : ISpotifyService
{
    private readonly ISpotifyIntegrationClient _spotifyClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICookieAuthorizationHandler _cookieAuthorizationHandler;
    private readonly ISpotifyProfilesRepository _spotifyProfilesRepository;

    public SpotifyService(
        ISpotifyIntegrationClient spotifyClient,
        IHttpContextAccessor httpContextAccessor,
        ICookieAuthorizationHandler cookieAuthorizationHandler,
        ISpotifyProfilesRepository spotifyProfilesRepository)
    {
        _spotifyClient = spotifyClient;
        _httpContextAccessor = httpContextAccessor;
        _cookieAuthorizationHandler = cookieAuthorizationHandler;
        _spotifyProfilesRepository = spotifyProfilesRepository;
    }

    public Uri GetAuthenticationUrl()
    {
        return _spotifyClient.GetAuthenticationUri();
    }

    public async Task<ErrorOr<Success<UserDto>>> AuthenticateByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var result =  await _spotifyClient.AuthenticateAsync(code, cancellationToken);

        Authorize(result);

        await _spotifyProfilesRepository.CreateProfileAsync(result, cancellationToken);

        var user = new UserDto
        {
            Id = result.ProfileId,
            AccessToken = result.AccessToken,
        };

        return new Success<UserDto>(user);
    }

    public async Task<ErrorOr<string>> AuthenticateByProfileIdAsync(int id, CancellationToken cancellationToken)
    {
        var profileResult = await _spotifyProfilesRepository.GetProfileAsync(id, cancellationToken);

        if (profileResult.IsError)
        {
            return profileResult.FirstError;
        }

        var result = await _spotifyClient.ReAuthenticateAsync(profileResult.Value.RefreshToken, cancellationToken);

        _cookieAuthorizationHandler.Authorize(result.AccessToken, result.ExpiresIn, _httpContextAccessor.HttpContext);

        return result.AccessToken;
    }

    public Task<string> GetCurrentlyPlayingAsync(string accessToken, CancellationToken cancellationToken)
    {
        return _spotifyClient.GetCurrentlyPlayingAsync(accessToken, cancellationToken);
    }

    private void Authorize(AuthenticationResult result)
    {
        var context = _httpContextAccessor.HttpContext;
        _cookieAuthorizationHandler.Authorize(result.ProfileId, context);
        _cookieAuthorizationHandler.Authorize(result.AccessToken, result.ExpiresIn, context);
    }
}