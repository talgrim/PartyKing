using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure;

public static class DependencyInjection
{
    public static void RegisterInfrastructure(this IServiceCollection services)
    {
        services.RegisterDatabase();
        services.RegisterRepositories();
    }

    private static void RegisterDatabase(this IServiceCollection services)
    {
        services.AddPooledDbContextFactory<WriterDbContext>(options =>
            {
                options.UseSqlite();
            })
            .AddPooledDbContextFactory<ReaderDbContext>(options =>
            {
                options.UseSqlite()
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            });
    }

    private static void RegisterRepositories(this IServiceCollection services)
    {
        services.AddSingleton<ISlideshowImagesRepository, SlideshowImagesRepository>();
    }
}