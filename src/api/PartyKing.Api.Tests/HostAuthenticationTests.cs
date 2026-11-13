using ErrorOr;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.API;
using PartyKing.API.Authentication;
using PartyKing.API.Cookies;
using PartyKing.API.Metrics;
using PartyKing.Application.Spotify.Models;
using PartyKing.Application.Spotify.Services;
using PartyKing.Contract.V1.Spotify;
using System.Security.Claims;

namespace PartyKing.Api.Tests;

/// <summary>
/// The host scheme with a fake Spotify service: the signed profile cookie is the identity, and a host without the
/// access token cookie gets one from the Spotify service, which in the demo is a placeholder.
/// </summary>
public class HostAuthenticationTests : IDisposable
{
    private readonly FakeSpotifyService _spotify = new();
    private readonly ServiceProvider _provider;
    private readonly SignedCookies _cookies;

    public HostAuthenticationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWebEncoders();
        services.AddSingleton<IDataProtectionProvider>(DataProtectionProvider.Create("partyking-tests"));
        services.AddSingleton<AuthorizationMetrics>();
        services.AddSingleton<ISpotifyService>(_spotify);
        DependencyInjection.AddAuthorization(services);

        _provider = services.BuildServiceProvider();
        _cookies = (SignedCookies)_provider.GetRequiredService<ISignedCookies>();
    }

    public void Dispose() => _provider.Dispose();

    private HttpContext RequestWith(string? profileId, string? accessToken)
    {
        var http = new DefaultHttpContext { RequestServices = _provider.CreateScope().ServiceProvider };
        var cookies = new List<string>();
        if (profileId is not null)
        {
            var response = new DefaultHttpContext();
            _cookies.GrantHost(profileId, response);
            cookies.Add(response.Response.Headers.SetCookie.ToString().Split(';')[0]);
        }

        if (accessToken is not null)
        {
            cookies.Add($"{CookieNames.Auth}={accessToken}");
        }

        http.Request.Headers.Cookie = string.Join("; ", cookies);

        return http;
    }

    [Fact]
    public async Task AHostWithBothCookies_IsAuthenticated_WithoutAskingSpotify()
    {
        var http = RequestWith("host-1", "cookie-token");

        var result = await http.AuthenticateAsync(SchemeNames.Host);

        Assert.True(result.Succeeded);
        Assert.Equal("host-1", result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("cookie-token", result.Principal.FindFirstValue(ClaimTypes.Authentication));
        Assert.Empty(_spotify.AskedFor);
    }

    [Fact]
    public async Task AHostWithoutTheAccessTokenCookie_GetsOneFromSpotify_AndTheResponseSetsIt()
    {
        _spotify.Grant = new SpotifyAccessGrant("fresh-token", 3600);
        var http = RequestWith("host-1", null);

        var result = await http.AuthenticateAsync(SchemeNames.Host);

        Assert.True(result.Succeeded);
        Assert.Equal("fresh-token", result.Principal!.FindFirstValue(ClaimTypes.Authentication));
        Assert.Equal(["host-1"], _spotify.AskedFor);
        Assert.Contains(http.Response.Headers.SetCookie, x => x!.StartsWith($"{CookieNames.Auth}=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AHostSpotifyDoesNotKnow_IsNotAuthenticated_AndSetsNoCookie()
    {
        _spotify.Grant = Error.NotFound(description: "No such profile");
        var http = RequestWith("host-gone", null);

        var result = await http.AuthenticateAsync(SchemeNames.Host);

        Assert.False(result.Succeeded);
        Assert.Equal("No such profile", result.Failure!.Message);
        Assert.Equal(0, http.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public async Task WithoutTheSignedProfileCookie_NobodyIsAuthenticated_EvenWithAnAccessToken()
    {
        var http = RequestWith(null, "cookie-token");

        var result = await http.AuthenticateAsync(SchemeNames.Host);

        Assert.False(result.Succeeded);
        Assert.Empty(_spotify.AskedFor);
    }

    private sealed class FakeSpotifyService : ISpotifyService
    {
        public ErrorOr<SpotifyAccessGrant> Grant { get; set; } = new SpotifyAccessGrant("token", 3600);
        public List<string> AskedFor { get; } = [];

        public Task<ErrorOr<SpotifyAccessGrant>> AuthenticateByProfileIdAsync(string id, CancellationToken cancellationToken)
        {
            AskedFor.Add(id);
            return Task.FromResult(Grant);
        }

        public Task<ErrorOr<Uri>> GetHostLoginUrlAsync(Guid sessionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ErrorOr<HostLogin>> AuthenticateByCodeAsync(string code, string state, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ErrorOr<IReadOnlyList<SpotifyTrackDto>>> SearchTracksAsync(
            Guid sessionId,
            string query,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
