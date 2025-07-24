using Microsoft.Extensions.DependencyInjection;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Application.System;

namespace PartyKing.Application;

public static class DependencyInjection
{
    public static void RegisterApplication(this IServiceCollection services)
    {
        services.RegisterSystem();
        services.RegisterSlideshow();
    }

    private static void RegisterSystem(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
    }

    private static void RegisterSlideshow(this IServiceCollection services)
    {
        services.AddSingleton<ISlideshowService, SlideshowService>();
    }
}