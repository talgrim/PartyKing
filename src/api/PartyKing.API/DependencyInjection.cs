using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using PartyKing.API.Authentication;
using PartyKing.API.Authentication.Cookie;
using PartyKing.API.Hubs;
using PartyKing.API.Metrics;
using PartyKing.Application;
using PartyKing.Application.Realtime;
using PartyKing.Infrastructure;
using PartyKing.Infrastructure.Authentication;
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
        services.RegisterInfrastructure();
        services.RegisterSpotifyInfrastructure(configuration);
        services.RegisterYoutubeInfrastructure();
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

    private static void AddAuthorization(this IServiceCollection services)
    {
        services
            .AddAuthentication()
            .AddScheme<CookieAuthenticationOptions, CookieAuthenticationHandler>(Constants.Scheme, null)
            .AddScheme<CookieAuthenticationOptions, AdminCookieAuthenticationHandler>(Constants.AdminScheme, null);

        services
            .AddAuthorizationBuilder()
            .AddPolicy(Policies.AuthenticatedUser, policy =>
            {
                policy.RequireAuthenticatedUser();
            })
            .AddPolicy(Policies.Admin, policy =>
            {
                policy.AddAuthenticationSchemes(Constants.AdminScheme);
                policy.RequireAuthenticatedUser();
            });
    }

    private static void RegisterHubs(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<ISessionMusicNotifier, SignalRSessionMusicNotifier>();
    }
}