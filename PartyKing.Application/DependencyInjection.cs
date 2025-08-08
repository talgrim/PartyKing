using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Application.Configuration;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Application.System;

namespace PartyKing.Application;

public static class DependencyInjection
{
    public static void RegisterApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.RegisterSystem();
        services.RegisterSlideshow(configuration);
    }

    private static void RegisterSystem(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
    }

    private static void RegisterSlideshow(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISlideshowService, SlideshowService>();
        services.Configure<SlideshowSettings>(configuration.GetSection(SlideshowSettings.SectionName));
    }
}