using PartyKing.Application;
using PartyKing.Infrastructure;
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
    }
}