using PartyKing.Application.Ports;
using PartyKing.Domain.Repositories;
using ErrorOr;
using Microsoft.Extensions.Logging;
using PartyKing.Application.Mappings;
using PartyKing.Application.Spotify.Models;
using PartyKing.Application.Sessions.Helpers;
using PartyKing.Contract.V1.Spotify;
using PartyKing.Domain.Enums;
using PartyKing.Domain.Results;

namespace PartyKing.Application.Spotify.Services;

public interface ISpotifyService
{
    /// <summary>Login URL for becoming (or, for the current host, logging in again as) the host of a session.</summary>
    Task<ErrorOr<Uri>> GetHostLoginUrlAsync(Guid sessionId, CancellationToken cancellationToken);
    /// <summary>Completes the host login and claims the session; the caller stores the login in cookies.</summary>
    Task<ErrorOr<HostLogin>> AuthenticateByCodeAsync(string code, string state, CancellationToken cancellationToken);

    /// <summary>Refreshes the access token of a host that is already known; the caller stores it in a cookie.</summary>
    Task<ErrorOr<SpotifyAccessGrant>> AuthenticateByProfileIdAsync(string id, CancellationToken cancellationToken);
    Task<ErrorOr<IReadOnlyList<SpotifyTrackDto>>> SearchTracksAsync(
        Guid sessionId,
        string query,
        CancellationToken cancellationToken);
}

internal class SpotifyService : ISpotifyService
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    private readonly ISpotifyIntegrationClient _spotifyClient;
    private readonly ISpotifyProfilesRepository _spotifyProfilesRepository;
    private readonly IHostTokenProvider _hostTokenProvider;
    private readonly ISessionsRepository _sessionsRepository;
    private readonly IOAuthStatesRepository _oAuthStatesRepository;
    private readonly ILogger<SpotifyService> _logger;

    public SpotifyService(
        ISpotifyIntegrationClient spotifyClient,
        ISpotifyProfilesRepository spotifyProfilesRepository,
        IHostTokenProvider hostTokenProvider,
        ISessionsRepository sessionsRepository,
        IOAuthStatesRepository oAuthStatesRepository,
        ILogger<SpotifyService> logger)
    {
        _spotifyClient = spotifyClient;
        _spotifyProfilesRepository = spotifyProfilesRepository;
        _hostTokenProvider = hostTokenProvider;
        _sessionsRepository = sessionsRepository;
        _oAuthStatesRepository = oAuthStatesRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<Uri>> GetHostLoginUrlAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _sessionsRepository.GetAsync(sessionId, cancellationToken);

        if (session is null)
        {
            return SessionErrors.NotFound();
        }

        // Allowed even when the session has a host: the host needs it to log in again after the Spotify
        // session expires. Someone else going through it is rejected when the session is claimed.
        var state = await _oAuthStatesRepository.CreateAsync(sessionId, StateLifetime, cancellationToken);

        return _spotifyClient.GetAuthenticationUri(state);
    }

    public async Task<ErrorOr<HostLogin>> AuthenticateByCodeAsync(
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
        catch (SpotifyApiException)
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
                _logger.LogWarning("Host login rejected: session {SessionId} already has a host", sessionId);
                return Error.Conflict(description: "This session already has a host");
            case SessionClaimResult.ProfileHostsAnotherSession:
                _logger.LogWarning("Host login rejected: the Spotify account already hosts another session ({SessionId})", sessionId);
                return Error.Conflict(description: "This Spotify account already hosts another session");
        }

        _logger.LogInformation("A host logged in to session {SessionId}", sessionId);

        return new HostLogin(result.ProfileId, result.AccessToken, result.ExpiresIn, sessionId.Value);
    }

    public async Task<ErrorOr<SpotifyAccessGrant>> AuthenticateByProfileIdAsync(string id, CancellationToken cancellationToken)
    {
        var profile = await _spotifyProfilesRepository.GetProfileAsync(id, cancellationToken);

        if (profile is null)
        {
            return SpotifyErrors.ProfileNotFound(id);
        }

        var result = await _spotifyClient.ReAuthenticateAsync(profile.RefreshToken, cancellationToken);

        return new SpotifyAccessGrant(result.AccessToken, result.ExpiresIn);
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
}