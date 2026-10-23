using System.Collections.Concurrent;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Application.System;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Spotify.RestApi;
using SpotifyAPI.Web;

namespace PartyKing.Application.Spotify.Services;

/// <summary>
/// Provides a Spotify access token for a session's host account, so guests (who aren't logged in) can search
/// and queue Spotify songs. Tokens are cached per host until shortly before they expire.
/// </summary>
public interface IHostTokenProvider
{
    Task<ErrorOr<string>> GetSessionAccessTokenAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<ErrorOr<string>> GetAccessTokenAsync(string profileId, CancellationToken cancellationToken);

    /// <summary>Drops the cached token of a host account, e.g. when the host is released from a session.</summary>
    void Invalidate(string profileId);
}

internal class HostTokenProvider : IHostTokenProvider
{
    private static readonly TimeSpan ExpirationMargin = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISpotifyProfilesRepository _profilesRepository;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ConcurrentDictionary<string, CachedToken> _tokens = new();

    public HostTokenProvider(
        IServiceScopeFactory scopeFactory,
        ISpotifyProfilesRepository profilesRepository,
        ISessionsRepository sessionsRepository,
        IDateTimeProvider dateTimeProvider)
    {
        _scopeFactory = scopeFactory;
        _profilesRepository = profilesRepository;
        _sessionsRepository = sessionsRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<ErrorOr<string>> GetSessionAccessTokenAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(sessionId, cancellationToken);

        if (session is null)
        {
            return Error.NotFound(description: "The session was not found");
        }

        if (session.HostProfileId is null)
        {
            return Error.Conflict(description: "The session has no host yet");
        }

        return await GetAccessTokenAsync(session.HostProfileId, cancellationToken);
    }

    public async Task<ErrorOr<string>> GetAccessTokenAsync(string profileId, CancellationToken cancellationToken)
    {
        var cached = _tokens.GetOrAdd(profileId, _ => new CachedToken());

        await cached.Lock.WaitAsync(cancellationToken);
        try
        {
            if (cached.AccessToken is not null && _dateTimeProvider.UtcNow < cached.ExpiresAt)
            {
                return cached.AccessToken;
            }

            var profile = await _profilesRepository.GetProfileAsync(profileId, cancellationToken);
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
                // The refresh token is dead, so there is nothing worth caching for this account
                _tokens.TryRemove(profileId, out _);

                return Error.Unauthorized(description: "The Spotify host session expired. The host must log in again");
            }

            cached.AccessToken = result.AccessToken;
            cached.ExpiresAt = _dateTimeProvider.UtcNow.AddSeconds(result.ExpiresIn) - ExpirationMargin;

            return cached.AccessToken;
        }
        finally
        {
            cached.Lock.Release();
        }
    }

    public void Invalidate(string profileId)
    {
        _tokens.TryRemove(profileId, out _);
    }

    private sealed class CachedToken
    {
        public SemaphoreSlim Lock { get; } = new(1, 1);
        public string? AccessToken { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
