using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Application.Identity;
using PartyKing.Application.Ports;
using PartyKing.Infrastructure;
using PartyKing.Infrastructure.Identity;

namespace PartyKing.Application.Tests;

/// <summary>
/// The identity service is used unless <c>Identity:Enabled</c> is off; the demo has no accounts, so it never calls the
/// service and nobody is ever signed in.
/// </summary>
public class IdentityRegistrationTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        // The same order as the API: the identity registration comes after the Application's, which adds the null client
        services.RegisterApplication(configuration);
        services.RegisterIdentityInfrastructure(configuration);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void ByDefault_TheIdentityServiceIsAsked()
    {
        using var provider = Build([]);

        Assert.IsType<IdentityClient>(provider.GetRequiredService<IIdentityClient>());
    }

    [Fact]
    public void WhenDisabled_NobodyIsEverSignedIn()
    {
        using var provider = Build(new() { ["Identity:Enabled"] = "false" });

        Assert.IsType<NullIdentityClient>(provider.GetRequiredService<IIdentityClient>());
    }

    [Fact]
    public async Task TheNullClient_AnswersAnonymous_WhateverTheCookie()
    {
        using var provider = Build(new() { ["Identity:Enabled"] = "false" });

        var result = await provider.GetRequiredService<IIdentityClient>()
            .GetCallerAsync("__Secure-talgrim.session=abc", null, CancellationToken.None);

        Assert.IsType<IdentityResult.Anonymous>(result);
    }

    [Fact]
    public void TheClientIsOneInstance_SoTheCacheIsShared()
    {
        using var provider = Build([]);

        Assert.Same(provider.GetRequiredService<IIdentityClient>(), provider.GetRequiredService<IIdentityClient>());
    }
}
