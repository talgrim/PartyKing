using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Infrastructure.Configuration;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure;

public static class DependencyInjection
{
    public static void RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.RegisterDatabase();
        services.RegisterRepositories(configuration);
    }

    public static async Task MigrateDb(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<WriterDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.Database.MigrateAsync();
    }

    private static void RegisterDatabase(this IServiceCollection services)
    {
        services.AddDbContextFactory<WriterDbContext>();
        services.AddDbContextFactory<ReaderDbContext>();
    }

    private static void RegisterRepositories(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SlideshowSettings>(configuration.GetSection(SlideshowSettings.SectionName));
        services.AddSingleton<ISlideshowImagesRepository, SlideshowImagesRepository>();
    }
}