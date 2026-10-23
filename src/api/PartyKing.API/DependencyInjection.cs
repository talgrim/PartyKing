using Microsoft.AspNetCore.DataProtection;
using PartyKing.API.Authentication;
using PartyKing.API.Authentication.Cookie;
using PartyKing.API.Metrics;
using PartyKing.Application;
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
        }

        services.RegisterApplication(configuration);
        services.RegisterInfrastructure();
        services.RegisterSpotifyInfrastructure(configuration);
        services.RegisterYoutubeInfrastructure();
        services.AddAuthorization();
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        AddConfigurations(configuration);
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
}