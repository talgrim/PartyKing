using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Application.System;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Spotify.RestApi;
using SpotifyAPI.Web;

namespace PartyKing.Application.Spotify.Services;

/// <summary>
/// Provides a Spotify access token for the host account, so guests (who aren't logged in) can search
/// and queue Spotify songs. The token is cached until shortly before it expires.
/// </summary>
public interface IHostTokenProvider
{
    Task<ErrorOr<string>> GetAccessTokenAsync(CancellationToken cancellationToken);
}

internal class HostTokenProvider : IHostTokenProvider
{
    private static readonly TimeSpan ExpirationMargin = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISpotifyProfilesRepository _profilesRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    public HostTokenProvider(
        IServiceScopeFactory scopeFactory,
        ISpotifyProfilesRepository profilesRepository,
        IDateTimeProvider dateTimeProvider)
    {
        _scopeFactory = scopeFactory;
        _profilesRepository = profilesRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<ErrorOr<string>> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && _dateTimeProvider.UtcNow < _expiresAt)
            {
                return _accessToken;
            }

            var profile = await _profilesRepository.GetHostProfileAsync(cancellationToken);
            if (profile.IsError)
            {
                return profile.FirstError;
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var client = scope.ServiceProvider.GetRequiredService<ISpotifyIntegrationClient>();

            ReAuthenticationResult result;
            try
            {
                result = await client.ReAuthenticateAsync(profile.Value.RefreshToken, cancellationToken);
            }
            catch (APIException)
            {
                return Error.Unauthorized(description: "The Spotify host session expired. The host must log in again");
            }

            _accessToken = result.AccessToken;
            _expiresAt = _dateTimeProvider.UtcNow.AddSeconds(result.ExpiresIn) - ExpirationMargin;

            return _accessToken;
        }
        finally
        {
            _lock.Release();
        }
    }
}
