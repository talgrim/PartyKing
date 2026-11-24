using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Application;
using PartyKing.Application.Mail;
using PartyKing.Application.Ports;
using PartyKing.Infrastructure.Mail;

namespace PartyKing.Application.Tests;

/// <summary>
/// The mail provider is used only when the key and the sender are both configured; everywhere else (local development,
/// the e2e environment, the demo) the no-op client keeps real mail from being sent.
/// </summary>
public class MailRegistrationTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        // The same order as the API: the mail registration comes after the Application's, which adds the no-op client
        services.RegisterApplication(configuration);
        services.RegisterMailInfrastructure(configuration);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void WithoutAnyMailSettings_NothingIsSent()
    {
        using var provider = Build([]);

        Assert.IsType<NullMailClient>(provider.GetRequiredService<IMailClient>());
    }

    [Theory]
    [InlineData("a-key", null)]
    [InlineData("a-key", "")]
    [InlineData("a-key", "  ")]
    [InlineData(null, "partyking-requests@talgrim.dev")]
    [InlineData("", "partyking-requests@talgrim.dev")]
    [InlineData("  ", "partyking-requests@talgrim.dev")]
    public void WithoutBothTheKeyAndTheSender_NothingIsSent(string? apiKey, string? sender)
    {
        using var provider = Build(new()
        {
            ["MailSettings:ApiKey"] = apiKey,
            ["MailSettings:Sender"] = sender
        });

        Assert.IsType<NullMailClient>(provider.GetRequiredService<IMailClient>());
    }

    [Fact]
    public void WithTheKeyAndTheSender_TheResendClientIsUsed()
    {
        using var provider = Build(new()
        {
            ["MailSettings:ApiKey"] = "a-key",
            ["MailSettings:Sender"] = "partyking-requests@talgrim.dev"
        });

        using var scope = provider.CreateScope();

        Assert.IsType<ResendMailClient>(scope.ServiceProvider.GetRequiredService<IMailClient>());
    }

    [Fact]
    public async Task TheResendClient_SendsTheKeyAsABearerToken_AndSaysWhoItIs()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MailSettings:ApiKey"] = "a-key",
            ["MailSettings:Sender"] = "partyking-requests@talgrim.dev"
        }).Build();
        var seen = new List<HttpRequestMessage>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.RegisterApplication(configuration);
        services.RegisterMailInfrastructure(configuration);
        services.AddHttpClient<IMailClient, ResendMailClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new CapturingHandler(seen));
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IMailClient>().SendAsync(new MailMessage("a@b.c", "s", "b"), CancellationToken.None);

        var request = Assert.Single(seen);
        Assert.Equal("https://api.resend.com/emails", request.RequestUri?.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("a-key", request.Headers.Authorization?.Parameter);
        Assert.NotEmpty(request.Headers.UserAgent);
    }

    private sealed class CapturingHandler(List<HttpRequestMessage> seen) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            seen.Add(request);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        }
    }
}
