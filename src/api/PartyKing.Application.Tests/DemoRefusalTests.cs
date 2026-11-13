using ErrorOr;
using PartyKing.Application.Demo.Services;

namespace PartyKing.Application.Tests;

/// <summary>The demo has no Spotify: what would reach it is refused with a message the UI can show.</summary>
public class DemoRefusalTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

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
        var service = new DemoSpotifyService();

        AssertRefused((await service.GetHostLoginUrlAsync(Guid.NewGuid(), Ct)).Errors, "The Spotify login");
        AssertRefused((await service.AuthenticateByCodeAsync("code", "state", Ct)).Errors, "The Spotify login");
        AssertRefused((await service.AuthenticateByProfileIdAsync("demo-host", Ct)).Errors, "The Spotify login");
    }

    [Fact]
    public async Task TheSpotifySearch_IsRefused()
    {
        var result = await new DemoSpotifyService().SearchTracksAsync(Guid.NewGuid(), "abba", Ct);

        AssertRefused(result.Errors, "Spotify search");
    }
}
