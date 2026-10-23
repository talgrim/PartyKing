using ErrorOr;
using Microsoft.AspNetCore.Http;
using PartyKing.Application.Mappings;
using PartyKing.Contract.V1.Spotify;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Authorization;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Spotify.RestApi;
using SpotifyAPI.Web;

namespace PartyKing.Application.Spotify.Services;

public interface ISpotifyService
{
    /// <summary>Login URL for becoming (or, for the current host, logging in again as) the host of a session.</summary>
    Task<ErrorOr<Uri>> GetHostLoginUrlAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<ErrorOr<UserDto>> AuthenticateByCodeAsync(string code, string state, CancellationToken cancellationToken);
    Task<ErrorOr<string>> AuthenticateByProfileIdAsync(string id, CancellationToken cancellationToken);
    Task<ErrorOr<IReadOnlyList<SpotifyTrackDto>>> SearchTracksAsync(
        Guid sessionId,
        string query,
        CancellationToken cancellationToken);
}

internal class SpotifyService : ISpotifyService
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    private readonly ISpotifyIntegrationClient _spotifyClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICookieAuthorizationHandler _cookieAuthorizationHandler;
    private readonly ISpotifyProfilesRepository _spotifyProfilesRepository;
    private readonly IHostTokenProvider _hostTokenProvider;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly IOAuthStatesRepository _oAuthStatesRepository;

    public SpotifyService(
        ISpotifyIntegrationClient spotifyClient,
        IHttpContextAccessor httpContextAccessor,
        ICookieAuthorizationHandler cookieAuthorizationHandler,
        ISpotifyProfilesRepository spotifyProfilesRepository,
        IHostTokenProvider hostTokenProvider,
        ISessionsRepository sessionsRepository,
        IOAuthStatesRepository oAuthStatesRepository)
    {
        _spotifyClient = spotifyClient;
        _httpContextAccessor = httpContextAccessor;
        _cookieAuthorizationHandler = cookieAuthorizationHandler;
        _spotifyProfilesRepository = spotifyProfilesRepository;
        _hostTokenProvider = hostTokenProvider;
        _sessionsRepository = sessionsRepository;
        _oAuthStatesRepository = oAuthStatesRepository;
    }

    public async Task<ErrorOr<Uri>> GetHostLoginUrlAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(sessionId, cancellationToken);

        if (session is null)
        {
            return Error.NotFound(description: "The session was not found");
        }

        // Allowed even when the session has a host: the host needs it to log in again after the Spotify
        // session expires. Someone else going through it is rejected when the session is claimed.
        var state = await _oAuthStatesRepository.CreateAsync(sessionId, StateLifetime, cancellationToken);

        return _spotifyClient.GetAuthenticationUri(state);
    }

    public async Task<ErrorOr<UserDto>> AuthenticateByCodeAsync(
        string code,
        string state,
        CancellationToken cancellationToken)
    {
        var sessionId = await _oAuthStatesRepository.ConsumeAsync(state, cancellationToken);

        if (sessionId is null)
        {
            return Error.Validation(description: "The login link expired or is invalid. Start the login again");
        }

        AuthenticationResult result;
        try
        {
            result = await _spotifyClient.AuthenticateAsync(code, cancellationToken);
        }
        catch (APIException)
        {
            return Error.Unauthorized(description: "Spotify rejected the login. Start the login again");
        }

        await _spotifyProfilesRepository.CreateProfileAsync(result, cancellationToken);

        var claim = await _sessionsRepository.TryClaimAsync(sessionId.Value, result.ProfileId, cancellationToken);

        switch (claim)
        {
            case SessionClaimResult.SessionNotFound:
                return Error.NotFound(description: "The session no longer exists");
            case SessionClaimResult.AlreadyClaimed:
                return Error.Conflict(description: "This session already has a host");
            case SessionClaimResult.ProfileHostsAnotherSession:
                return Error.Conflict(description: "This Spotify account already hosts another session");
        }

        Authorize(result);

        return new UserDto
        {
            Id = result.ProfileId,
            AccessToken = result.AccessToken,
            SessionId = sessionId.Value
        };
    }

    public async Task<ErrorOr<string>> AuthenticateByProfileIdAsync(string id, CancellationToken cancellationToken)
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

    public async Task<ErrorOr<IReadOnlyList<SpotifyTrackDto>>> SearchTracksAsync(
        Guid sessionId,
        string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Error.Validation(description: "Search query must not be empty");
        }

        var token = await _hostTokenProvider.GetSessionAccessTokenAsync(sessionId, cancellationToken);
        if (token.IsError)
        {
            return token.FirstError;
        }

        var songs = await _spotifyClient.SearchTracksAsync(token.Value, query.Trim(), cancellationToken);

        return songs.Select(x => x.ToDto()).ToList();
    }

    private void Authorize(AuthenticationResult result)
    {
        var context = _httpContextAccessor.HttpContext;
        _cookieAuthorizationHandler.Authorize(result.ProfileId, context);
        _cookieAuthorizationHandler.Authorize(result.AccessToken, result.ExpiresIn, context);
    }
}