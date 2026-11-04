using System.Net;
using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using PartyKing.Application.Ports;
using PartyKing.Application.Spotify.Services;
using PartyKing.Domain.Entities.Auth;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Repositories;
using PartyKing.Domain.Results;

namespace PartyKing.Application.Tests;

/// <summary>The OAuth state handling of the host login: the state is made with the clock and taken once.</summary>
public class SpotifyServiceTests
{
    private readonly FakeClock _clock = new();
    private readonly FakeSessionsRepository _sessions = new();
    private readonly FakeOAuthStatesRepository _states = new();
    private readonly SpotifyService _service;
    private readonly Guid _sessionId;

    public SpotifyServiceTests()
    {
        _service = new SpotifyService(
            new RejectingSpotifyClient(),
            new FakeProfilesRepository(),
            new FakeHostTokenProvider(),
            _sessions,
            _states,
            _clock,
            NullLogger<SpotifyService>.Instance);
        _sessionId = _sessions.AddSession().Id;
    }

    [Fact]
    public async Task TheLoginUrl_CarriesAStateThatLivesTenMinutesFromNow()
    {
        var result = await _service.GetHostLoginUrlAsync(_sessionId, CancellationToken.None);

        var state = Assert.Single(_states.Saved);
        Assert.Equal(new Uri($"https://spotify.test/authorize?state={state.Id}"), result.Value);
        Assert.Equal(_sessionId, state.SessionId);
        Assert.Equal(_clock.UtcNow.UtcDateTime.AddMinutes(10), state.ExpiresAt);
        Assert.Equal(_clock.UtcNow.UtcDateTime, _states.LastNow);
    }

    [Fact]
    public async Task TheLoginUrl_OfAnUnknownSession_IsNotFoundAndMakesNoState()
    {
        var result = await _service.GetHostLoginUrlAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.FirstError.Type);
        Assert.Empty(_states.Saved);
    }

    [Fact]
    public async Task AnUnknownState_IsRefused()
    {
        var result = await _service.AuthenticateByCodeAsync("code", "unknown", CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task AnExpiredState_IsRefused_AndUsedUp()
    {
        await _service.GetHostLoginUrlAsync(_sessionId, CancellationToken.None);
        var state = _states.Saved.Single();
        _clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        var result = await _service.AuthenticateByCodeAsync("code", state.Id, CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
        Assert.Empty(_states.Saved);
    }

    [Fact]
    public async Task AStateStillInTime_GetsPastTheStateCheck_AndIsUsedUp()
    {
        await _service.GetHostLoginUrlAsync(_sessionId, CancellationToken.None);
        var state = _states.Saved.Single();
        _clock.Advance(TimeSpan.FromMinutes(10));

        var result = await _service.AuthenticateByCodeAsync("code", state.Id, CancellationToken.None);

        // Spotify rejects the code, so the state check was passed; the state is burnt either way
        Assert.Equal(ErrorType.Unauthorized, result.FirstError.Type);
        Assert.Empty(_states.Saved);
    }

    private sealed class FakeOAuthStatesRepository : IOAuthStatesRepository
    {
        public List<OAuthState> Saved { get; } = [];
        public DateTime? LastNow { get; private set; }

        public Task AddAsync(OAuthState state, DateTime now, CancellationToken cancellationToken)
        {
            LastNow = now;
            Saved.Add(state);
            return Task.CompletedTask;
        }

        public Task<OAuthState?> TakeAsync(string id, CancellationToken cancellationToken)
        {
            var state = Saved.FirstOrDefault(x => x.Id == id);
            if (state is not null)
            {
                Saved.Remove(state);
            }

            return Task.FromResult(state);
        }
    }

    private sealed class RejectingSpotifyClient : ISpotifyIntegrationClient
    {
        public Uri GetAuthenticationUri(string state) => new($"https://spotify.test/authorize?state={state}");

        public Task<AuthenticationResult> AuthenticateAsync(string code, CancellationToken cancellationToken) =>
            throw new SpotifyApiException(HttpStatusCode.BadRequest, "invalid code", new Exception());

        public Task<ReAuthenticationResult> ReAuthenticateAsync(string refreshToken, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Song>> SearchTracksAsync(string accessToken, string query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Song?> GetTrackAsync(string accessToken, string trackId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Playlist?> GetPlaylistAsync(string accessToken, string playlistId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Playlist>> GetCurrentUserPlaylistsAsync(string accessToken, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Song>> GetPlaylistTracksAsync(string accessToken, string playlistId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeProfilesRepository : ISpotifyProfilesRepository
    {
        public Task<SpotifyProfile?> GetProfileAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CreateProfileAsync(AuthenticationResult result, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
