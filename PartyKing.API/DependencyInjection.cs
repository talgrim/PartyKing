using PartyKing.Application;
using PartyKing.Infrastructure;

namespace PartyKing.API;

public static class DependencyInjection
{
    public static void RegisterServices(this IServiceCollection services)
    {
        services.RegisterApplication();
        services.RegisterInfrastructure();
    }
}