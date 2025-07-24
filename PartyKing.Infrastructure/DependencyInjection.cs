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

    private static void RegisterRepositories(this IServiceCollection services)
    {
        services.AddSingleton<ISlideshowImagesRepository, SlideshowImagesRepository>();
    }
}