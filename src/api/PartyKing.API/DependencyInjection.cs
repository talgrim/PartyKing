using Identity.Client;
using Identity.Client.AspNetCore;
using Identity.Contract.V1.Enums;
using PartyKing.API.Cookies;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using PartyKing.API.Authentication;
using PartyKing.API.Authentication.Schemes;
using PartyKing.API.Demo;
using PartyKing.API.Hubs.Notifiers;
using PartyKing.API.Metrics;
using PartyKing.Application;
using PartyKing.Application.Configuration;
using PartyKing.Application.Realtime;
using PartyKing.Infrastructure;
using PartyKing.Infrastructure.Llm;
using PartyKing.API.Workers;
using PartyKing.Infrastructure.Lyrics;
using PartyKing.Infrastructure.Mail;
using PartyKing.Infrastructure.Spotify;
using PartyKing.Infrastructure.Youtube;

namespace PartyKing.API;

public static class DependencyInjection
{
    public static void RegisterServices(this IServiceCollection services, IConfigurationManager configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<ActionMetrics>();
        services.AddSingleton<AuthorizationMetrics>();
        services.AddScoped<ActionMetricsFilter>();
        var dataProtection = services.AddDataProtection().SetApplicationName("PartyKing");
        var keysDirectory = configuration["DataProtection:KeysDirectory"];
        if (!string.IsNullOrWhiteSpace(keysDirectory))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));

            // Keys on disk are encrypted with a certificate, so reading the volume or a backup is not enough to forge cookies.
            // Keys written before the certificate was configured stay readable and are replaced as they expire.
            var certificate = LoadKeyEncryptionCertificate(configuration)
                ?? throw new InvalidOperationException(
                    "DataProtection:KeysDirectory is set, so DataProtection:CertificateBase64 (a base64 PFX) is required to encrypt the keys.");
            dataProtection.ProtectKeysWithCertificate(certificate);
        }

        services.RegisterHubs();
        services.RegisterApplication(configuration);
        services.RegisterDemoWorker(configuration);
        // Not behind the demo flag, unlike the demo sweep: a guest's request is honoured on every server
        services.AddHostedService<SessionDeletionWorker>();
        services.RegisterInfrastructure();
        services.RegisterSpotifyInfrastructure(configuration);
        services.RegisterYoutubeInfrastructure();
        services.RegisterLyricsInfrastructure();
        services.RegisterLlmInfrastructure(configuration);
        // After RegisterApplication, which registers the no-op mail client this one replaces
        services.RegisterMailInfrastructure(configuration);
        services.RegisterIdentity(configuration);
        services.AddAuthorization();
        AddConfigurations(configuration);
    }

    private static X509Certificate2? LoadKeyEncryptionCertificate(IConfiguration configuration)
    {
        var base64 = configuration["DataProtection:CertificateBase64"];
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        return X509CertificateLoader.LoadPkcs12(
            Convert.FromBase64String(base64),
            configuration["DataProtection:CertificatePassword"]);
    }

    private static void AddConfigurations(
        IConfigurationManager configuration)
    {
        configuration.AddJsonFile("appsettings.Local.json", optional: true);
    }

    /// <summary>
    /// The accounts are the identity service's: its client (<c>Talgrim.Identity.Client</c>) asks it who a session cookie
    /// belongs to, and the verifier checks the events it sends. With <c>Identity:Enabled</c> off (the demo has no
    /// accounts) nobody is ever signed in and nothing is called.
    /// </summary>
    internal static void RegisterIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(IdentityConfiguration.SectionName);
        var settings = section.Get<IdentityConfiguration>() ?? new IdentityConfiguration();

        if (settings.Enabled)
        {
            services.AddIdentityClient(section)
                .ConfigureHttpClient(client => client.DefaultRequestHeaders.UserAgent.ParseAdd("PartyKing/1.0"));
        }
        else
        {
            // The handler and the dev personas still need the cookie name, which is the client's option
            services.AddOptions<IdentityClientOptions>().Bind(section);
            services.AddSingleton<IIdentityClient, NullIdentityClient>();
        }

        services.AddIdentityWebhookVerifier(options => options.Secret = settings.WebhookSecret);
    }

    internal static void AddAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<ISignedCookies, SignedCookies>();

        services
            .AddAuthentication()
            .AddScheme<CookieAuthenticationOptions, CookieAuthenticationHandler>(SchemeNames.Host, null)
            .AddScheme<AuthenticationSchemeOptions, AccountCookieAuthenticationHandler>(SchemeNames.Account, null);

        services
            .AddAuthorizationBuilder()
            .AddPolicy(Policies.AuthenticatedUser, policy =>
            {
                policy.RequireAuthenticatedUser();
            })
            .AddPolicy(Policies.Account, policy =>
            {
                policy.AddAuthenticationSchemes(SchemeNames.Account);
                policy.RequireAuthenticatedUser();
            })
            .AddPolicy(Policies.Admin, policy =>
            {
                policy.AddAuthenticationSchemes(SchemeNames.Account);
                policy.RequireAuthenticatedUser();
                policy.RequireRole(nameof(AccountRole.Admin));
            });
    }

    /// <summary>The sweep of expired demo sessions runs only on a server that is a demo.</summary>
    private static void RegisterDemoWorker(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetDemoConfiguration().Enabled)
        {
            services.AddHostedService<DemoSessionCleanupWorker>();
        }
    }

    private static void RegisterHubs(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<IQueueNotifier, SignalRQueueNotifier>();
        services.AddSingleton<IPlayerNotifier, SignalRPlayerNotifier>();
        services.AddSingleton<ILyricsNotifier, SignalRLyricsNotifier>();
        services.AddSingleton<ISlideshowNotifier, SignalRSlideshowNotifier>();
        services.AddSingleton<IRingoNotifier, SignalRRingoNotifier>();
        services.AddSingleton<IResistanceNotifier, SignalRResistanceNotifier>();
    }
}