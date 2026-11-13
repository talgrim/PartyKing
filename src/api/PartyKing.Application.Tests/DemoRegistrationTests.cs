using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Application.Demo.Services;
using PartyKing.Application.Spotify.Services;

namespace PartyKing.Application.Tests;

/// <summary>The demo swaps the services that reach Spotify, and only on a server that is a demo.</summary>
public class DemoRegistrationTests
{
    private static ServiceCollection Register(bool? enabled)
    {
        var settings = new Dictionary<string, string?>();
        if (enabled is not null)
        {
            settings["DemoSettings:Enabled"] = enabled.ToString();
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.RegisterApplication(configuration);

        return services;
    }

    private static Type ImplementationOf<T>(ServiceCollection services) =>
        Assert.Single(services, x => x.ServiceType == typeof(T)).ImplementationType!;

    [Fact]
    public void OnADemoServer_TheSpotifyServicesAreTheRefusingOnes()
    {
        var services = Register(enabled: true);

        Assert.Equal(typeof(DemoSpotifyService), ImplementationOf<ISpotifyService>(services));
        Assert.Equal(typeof(DemoHostTokenProvider), ImplementationOf<IHostTokenProvider>(services));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void OtherwiseTheyAreTheRealOnes(bool? enabled)
    {
        var services = Register(enabled);

        Assert.Equal(typeof(SpotifyService), ImplementationOf<ISpotifyService>(services));
        Assert.Equal(typeof(HostTokenProvider), ImplementationOf<IHostTokenProvider>(services));
    }

    [Fact]
    public void TheDemoServices_AreRegisteredEitherWay_SoTheConfigEndpointCanAnswerNo()
    {
        var services = Register(enabled: false);

        Assert.Single(services, x => x.ServiceType == typeof(IDemoSessionService));
        Assert.Single(services, x => x.ServiceType == typeof(IDemoSessionCleanup));
    }
}
