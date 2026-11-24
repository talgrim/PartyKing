using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PartyKing.Application.Configuration;
using PartyKing.Application.Ports;

namespace PartyKing.Infrastructure.Mail;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Resend client as <see cref="IMailClient"/> when <c>MailSettings:ApiKey</c> and
    /// <c>MailSettings:Sender</c> are both set. Otherwise nothing is registered and Application's
    /// <c>NullMailClient</c> stays: local development, the e2e environment and the demo never send real mail. Call it
    /// after <c>RegisterApplication</c>, so that this registration is the later one and wins.
    /// </summary>
    public static void RegisterMailInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(MailConfiguration.SectionName).Get<MailConfiguration>();

        if (string.IsNullOrWhiteSpace(settings?.ApiKey) || string.IsNullOrWhiteSpace(settings.Sender))
        {
            return;
        }

        services.AddHttpClient<IMailClient, ResendMailClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<MailConfiguration>>().Value;

            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

            // Resend refuses a request that does not say who sends it
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PartyKing/1.0");
        });
    }
}
