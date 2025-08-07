using PartyKing.Application;
using PartyKing.Infrastructure;

namespace PartyKing.API;

public static class DependencyInjection
{
    public static void RegisterServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.RegisterApplication();
        services.RegisterInfrastructure(configuration);
    }
}