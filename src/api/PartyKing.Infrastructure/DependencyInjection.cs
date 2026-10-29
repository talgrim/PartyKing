using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Infrastructure.Authorization;
using PartyKing.Infrastructure.Metrics;
using PartyKing.Infrastructure.Repositories;
using PartyKing.Persistence.Database;

namespace PartyKing.Infrastructure;

public static class DependencyInjection
{
    public static void RegisterInfrastructure(this IServiceCollection services)
    {
        services.RegisterDatabase();
        services.RegisterRepositories();
        services.AddAuthorization();
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
        services.AddSingleton<RepositoryMetrics>();
        services.AddMetered<ISlideshowImagesRepository, SlideshowImagesRepository>(
            (inner, metrics) => new MeteredSlideshowImagesRepository(inner, metrics));
        services.AddMetered<ISpotifyProfilesRepository, SpotifyProfilesRepository>(
            (inner, metrics) => new MeteredSpotifyProfilesRepository(inner, metrics));
        services.AddMetered<IQueueRepository, QueueRepository>(
            (inner, metrics) => new MeteredQueueRepository(inner, metrics));
        services.AddMetered<ISessionsRepository, SessionsRepository>(
            (inner, metrics) => new MeteredSessionsRepository(inner, metrics));
        services.AddMetered<IOAuthStatesRepository, OAuthStatesRepository>(
            (inner, metrics) => new MeteredOAuthStatesRepository(inner, metrics));
        services.AddMetered<IAdminAccountsRepository, AdminAccountsRepository>(
            (inner, metrics) => new MeteredAdminAccountsRepository(inner, metrics));
        services.AddMetered<ILyricsOffsetsRepository, LyricsOffsetsRepository>(
            (inner, metrics) => new MeteredLyricsOffsetsRepository(inner, metrics));
        services.AddMetered<IRingoGamesRepository, RingoGamesRepository>(
            (inner, metrics) => new MeteredRingoGamesRepository(inner, metrics));
    }

    // Repositories hold state, so the implementation stays a singleton and the decorator wraps that one instance
    private static void AddMetered<TService, TImplementation>(
        this IServiceCollection services,
        Func<TService, RepositoryMetrics, TService> decorate)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddSingleton<TImplementation>();
        services.AddSingleton(provider => decorate(
            provider.GetRequiredService<TImplementation>(),
            provider.GetRequiredService<RepositoryMetrics>()));
    }

    private static void AddAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<ICookieAuthorizationHandler, CookieAuthorizationHandler>();
    }
}