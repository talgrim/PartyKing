using ErrorOr;
using PartyKing.Application.Demo.Services;
using PartyKing.Application.Spotify.Models;
using PartyKing.Domain.Results;

namespace PartyKing.Application.Tests;

/// <summary>The demo has no Spotify: what would reach it is refused with a message the UI can show.</summary>
public class DemoRefusalTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly FakeSpotifyProfilesRepository _profiles = new();

    private DemoSpotifyService CreateSpotify() => new(_profiles);

    private static void AssertRefused(IReadOnlyList<Error> errors, string what)
    {
        var error = Assert.Single(errors);
        Assert.Equal(ErrorType.Forbidden, error.Type);
        Assert.Equal($"{what} is not available in the demo", error.Description);
    }

    [Fact]
    public async Task TheHostToken_ForASession_IsRefused()
    {
        var result = await new DemoHostTokenProvider().GetSessionAccessTokenAsync(Guid.NewGuid(), Ct);

        AssertRefused(result.Errors, "Spotify");
    }

    [Fact]
    public async Task TheHostToken_ForAProfile_IsRefused()
    {
        var result = await new DemoHostTokenProvider().GetAccessTokenAsync("demo-host", Ct);

        AssertRefused(result.Errors, "Spotify");
    }

    [Fact]
    public void ForgettingATokenThatWasNeverKept_DoesNothing()
    {
        new DemoHostTokenProvider().Invalidate("demo-host");
    }

    [Fact]
    public async Task TheSpotifyLogin_IsRefused()
    {
        var service = CreateSpotify();

        AssertRefused((await service.GetHostLoginUrlAsync(Guid.NewGuid(), Ct)).Errors, "The Spotify login");
        AssertRefused((await service.AuthenticateByCodeAsync("code", "state", Ct)).Errors, "The Spotify login");
    }

    [Fact]
    public async Task ADemoHostWithoutASpotifyToken_GetsThePlaceholderOne_ForAnHour()
    {
        await _profiles.CreateProfileAsync(new AuthenticationResult("demo-host", "Demo host", "demo", "refresh", 3600), Ct);

        var result = await CreateSpotify().AuthenticateByProfileIdAsync("demo-host", Ct);

        Assert.Equal(new SpotifyAccessGrant("demo", 3600), result.Value);
    }

    [Fact]
    public async Task ASweptDemoHost_GetsNoToken()
    {
        var result = await CreateSpotify().AuthenticateByProfileIdAsync("demo-gone", Ct);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.NotFound, error.Type);
    }

    [Fact]
    public async Task TheSpotifySearch_IsRefused()
    {
        var result = await CreateSpotify().SearchTracksAsync(Guid.NewGuid(), "abba", Ct);

        AssertRefused(result.Errors, "Spotify search");
    }
}
