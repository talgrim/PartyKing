using System.Text;
using Identity.Client;
using Identity.Client.AspNetCore;
using Identity.Contract.V1.Webhooks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PartyKing.API;

namespace PartyKing.Api.Tests;

/// <summary>How the server wires the identity client package: on by default, off in the demo, configured from <c>Identity</c>.</summary>
public class IdentityRegistrationTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.RegisterIdentity(configuration);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void ByDefault_TheIdentityServiceIsAsked()
    {
        using var provider = Build([]);

        Assert.IsNotType<NullIdentityClient>(provider.GetRequiredService<IIdentityClient>());
    }

    [Fact]
    public async Task WhenDisabled_NobodyIsEverSignedIn()
    {
        using var provider = Build(new() { ["Identity:Enabled"] = "false" });

        var client = provider.GetRequiredService<IIdentityClient>();

        Assert.IsType<NullIdentityClient>(client);
        Assert.IsType<IdentityResult.Anonymous>(
            await client.GetCallerAsync($"{new IdentityClientOptions().CookieName}=abc", null, CancellationToken.None));
    }

    [Fact]
    public void TheClient_IsASingleton_SoItsCacheIsShared()
    {
        using var provider = Build([]);

        Assert.Same(provider.GetRequiredService<IIdentityClient>(), provider.GetRequiredService<IIdentityClient>());
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void TheIdentitySection_IsTheOptionsOfTheClient_WhetherOrNotItIsEnabled(string enabled)
    {
        using var provider = Build(new()
        {
            ["Identity:Enabled"] = enabled,
            ["Identity:BaseUrl"] = "http://127.0.0.1:5051/",
            ["Identity:CookieName"] = "talgrim.session"
        });

        var options = provider.GetRequiredService<IOptions<IdentityClientOptions>>().Value;

        Assert.Equal("http://127.0.0.1:5051/", options.BaseUrl);
        Assert.Equal("talgrim.session", options.CookieName);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void TheWebhookVerifier_TakesItsSecretFromTheConfiguration(string enabled)
    {
        using var provider = Build(new() { ["Identity:Enabled"] = enabled, ["Identity:WebhookSecret"] = "shh" });
        var body = Encoding.UTF8.GetBytes("""{"type":"account.deleted"}""");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var request = new DefaultHttpContext().Request;
        request.Headers[WebhookSignature.TimestampHeader] = timestamp;
        request.Headers[WebhookSignature.SignatureHeader] = WebhookSignature.Sign("shh", timestamp, body);

        Assert.True(provider.GetRequiredService<IIdentityWebhookVerifier>().IsSigned(request, body));
    }

    [Fact]
    public void WithoutAWebhookSecret_NoCallIsAccepted()
    {
        using var provider = Build([]);
        var body = Encoding.UTF8.GetBytes("""{"type":"account.deleted"}""");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var request = new DefaultHttpContext().Request;
        request.Headers[WebhookSignature.TimestampHeader] = timestamp;
        request.Headers[WebhookSignature.SignatureHeader] = WebhookSignature.Sign("", timestamp, body);

        Assert.False(provider.GetRequiredService<IIdentityWebhookVerifier>().IsSigned(request, body));
    }
}
