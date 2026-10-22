using PartyKing.API.Authentication;
using PartyKing.API.Authentication.Cookie;
using PartyKing.Application;
using PartyKing.Infrastructure;
using PartyKing.Infrastructure.Authentication;
using PartyKing.Infrastructure.Spotify;

namespace PartyKing.API;

public static class DependencyInjection
{
    public static void RegisterServices(this IServiceCollection services, IConfigurationManager configuration)
    {
        services.AddHttpContextAccessor();
        services.RegisterApplication(configuration);
        services.RegisterInfrastructure();
        services.RegisterSpotifyInfrastructure(configuration);
        services.AddAuthorization();
        AddConfigurations(configuration);
    }

    private static void AddConfigurations(
        IConfigurationManager configuration)
    {
        configuration.AddJsonFile("appsettings.Local.json");
    }

    private static void AddAuthorization(this IServiceCollection services)
    {
        services
            .AddAuthentication()
            .AddScheme<CookieAuthenticationOptions, CookieAuthenticationHandler>(Constants.Scheme, null);

        services
            .AddAuthorizationBuilder()
            .AddPolicy(Policies.AuthenticatedUser, policy =>
            {
                policy.RequireAuthenticatedUser();
            });
    }
}