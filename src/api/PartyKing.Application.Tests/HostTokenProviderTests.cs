using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Application.Spotify.Services;
using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Results;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Infrastructure.Spotify.RestApi;

namespace PartyKing.Application.Tests;

public class HostTokenProviderTests
{
    private const string ProfileId = "host-profile";

    private readonly FakeClock _clock = new();
    private readonly FakeSpotifyClient _spotify = new();
    private readonly HostTokenProvider _provider;

    public HostTokenProviderTests()
    {
        _provider = new HostTokenProvider(
            new FakeScopeFactory(_spotify),
            new FakeProfilesRepository(),
            new FakeSessionsRepository(),
            _clock);
    }

    [Fact]
    public async Task ACachedToken_IsReusedWithoutRefreshing()
    {
        await _provider.GetAccessTokenAsync(ProfileId, CancellationToken.None);
        await _provider.GetAccessTokenAsync(ProfileId, CancellationToken.None);

        Assert.Equal(1, _spotify.RefreshCalls);
    }

    [Fact]
    public async Task Invalidate_MakesTheNextCallRefreshTheToken()
    {
        var first = await _provider.GetAccessTokenAsync(ProfileId, CancellationToken.None);

        _provider.Invalidate(ProfileId);
        var second = await _provider.GetAccessTokenAsync(ProfileId, CancellationToken.None);

        Assert.Equal(2, _spotify.RefreshCalls);
        Assert.NotEqual(first.Value, second.Value);
    }

    [Fact]
    public async Task Invalidate_OfAnotherProfile_KeepsTheCachedToken()
    {
        await _provider.GetAccessTokenAsync(ProfileId, CancellationToken.None);

        _provider.Invalidate("someone-else");
        await _provider.GetAccessTokenAsync(ProfileId, CancellationToken.None);

        Assert.Equal(1, _spotify.RefreshCalls);
    }

    [Fact]
    public void Invalidate_OfAnUnknownProfile_DoesNothing()
    {
        _provider.Invalidate("never-seen");
    }

    private class FakeSpotifyClient : ISpotifyIntegrationClient
    {
        public int RefreshCalls { get; private set; }

        public Task<ReAuthenticationResult> ReAuthenticateAsync(string refreshToken, CancellationToken cancellationToken)
        {
            RefreshCalls++;
            return Task.FromResult(new ReAuthenticationResult($"token-{RefreshCalls}", refreshToken, 3600));
        }

        public Uri GetAuthenticationUri(string state) => throw new NotSupportedException();
        public Task<AuthenticationResult> AuthenticateAsync(string code, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Song>> SearchTracksAsync(string accessToken, string query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Song?> GetTrackAsync(string accessToken, string trackId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Playlist?> GetPlaylistAsync(string accessToken, string playlistId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Playlist>> GetCurrentUserPlaylistsAsync(string accessToken, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Song>> GetPlaylistTracksAsync(string accessToken, string playlistId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private class FakeProfilesRepository : ISpotifyProfilesRepository
    {
        public Task<ErrorOr<SpotifyProfile>> GetProfileAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult<ErrorOr<SpotifyProfile>>(new SpotifyProfile(id, "refresh-token"));

        public Task CreateProfileAsync(AuthenticationResult result, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private class FakeScopeFactory : IServiceScopeFactory
    {
        private readonly ISpotifyIntegrationClient _client;

        public FakeScopeFactory(ISpotifyIntegrationClient client) => _client = client;

        public IServiceScope CreateScope() => new FakeScope(_client);

        private class FakeScope : IServiceScope, IServiceProvider
        {
            private readonly ISpotifyIntegrationClient _client;

            public FakeScope(ISpotifyIntegrationClient client) => _client = client;

            public IServiceProvider ServiceProvider => this;

            public object? GetService(Type serviceType) =>
                serviceType == typeof(ISpotifyIntegrationClient) ? _client : null;

            public void Dispose()
            {
            }
        }
    }
}
