using PartyKing.API.Configuration;
using PartyKing.Application;
using PartyKing.Infrastructure;

namespace PartyKing.API;

public static class DependencyInjection
{
    public static void RegisterServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.RegisterApi(configuration);
        services.RegisterApplication();
        services.RegisterInfrastructure();
    }

    private static void RegisterApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SlideshowSettings>(configuration.GetSection(SlideshowSettings.SectionName));
    }
}