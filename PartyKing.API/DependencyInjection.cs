using PartyKing.API.Authentication;
using PartyKing.API.Authentication.Cookie;
using PartyKing.Application;
using PartyKing.Infrastructure;
using PartyKing.Infrastructure.Authentication;
using PartyKing.Infrastructure.Authorization;
using PartyKing.Infrastructure.Spotify;

namespace PartyKing.API;

public static class DependencyInjection
{
    public static void RegisterServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.RegisterApplication(configuration);
        services.RegisterInfrastructure();
        services.RegisterSpotifyInfrastructure(configuration);
        services.AddAuthorization();
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