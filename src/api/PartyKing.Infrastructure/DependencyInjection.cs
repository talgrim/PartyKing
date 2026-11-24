using PartyKing.Application.Configuration;
using PartyKing.Application.Ports;
using PartyKing.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PartyKing.Infrastructure.Identity;
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
    }

    /// <summary>
    /// Registers the client of the identity service as <see cref="IIdentityClient"/> unless <c>Identity:Enabled</c> is
    /// off. Otherwise nothing is registered and Application's <c>NullIdentityClient</c> stays (the demo has no
    /// accounts). Call it after <c>RegisterApplication</c>, so that this registration is the later one and wins.
    /// </summary>
    public static void RegisterIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(IdentityConfiguration.SectionName).Get<IdentityConfiguration>()
            ?? new IdentityConfiguration();

        if (!settings.Enabled)
        {
            return;
        }

        services.AddSingleton<IdentityMetrics>();
        services.AddSingleton<IdentityCache>();
        services.AddSingleton<IIdentityClient, IdentityClient>();
        services.AddHttpClient(IdentityClient.HttpClientName, (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<IdentityConfiguration>>().Value;

            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PartyKing/1.0");
        });
    }

    public static async Task MigrateDb(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<WriterDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.Database.MigrateAsync();

        // WAL lets readers go on while a write is in progress and syncs once per checkpoint instead of per commit;
        // the mode is stored in the file, synchronous is per connection (see PartyKingDbContextBase)
        await dbContext.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    }

    private static void RegisterDatabase(this IServiceCollection services)
    {
        services.AddDbContextFactory<WriterDbContext>();
        services.AddDbContextFactory<ReaderDbContext>();
    }

    private static void RegisterRepositories(this IServiceCollection services)
    {
        services.AddSingleton<RepositoryMetrics>();
        services.AddSingleton<SlideshowImagesRepository>();
        services.AddSingleton(provider => new MeteredSlideshowImagesRepository(
            provider.GetRequiredService<SlideshowImagesRepository>(),
            provider.GetRequiredService<SlideshowImagesRepository>(),
            provider.GetRequiredService<RepositoryMetrics>()));
        services.AddSingleton<ISlideshowImagesRepository>(
            provider => provider.GetRequiredService<MeteredSlideshowImagesRepository>());
        services.AddSingleton<ISlideshowSettingsRepository>(
            provider => provider.GetRequiredService<MeteredSlideshowImagesRepository>());
        services.AddMetered<ISpotifyProfilesRepository, SpotifyProfilesRepository>(
            (inner, metrics) => new MeteredSpotifyProfilesRepository(inner, metrics));
        services.AddMetered<IQueueRepository, QueueRepository>(
            (inner, metrics) => new MeteredQueueRepository(inner, metrics));
        services.AddMetered<ISessionsRepository, SessionsRepository>(
            (inner, metrics) => new MeteredSessionsRepository(inner, metrics));
        services.AddMetered<IOAuthStatesRepository, OAuthStatesRepository>(
            (inner, metrics) => new MeteredOAuthStatesRepository(inner, metrics));
        services.AddMetered<IDemoSessionsRepository, DemoSessionsRepository>(
            (inner, metrics) => new MeteredDemoSessionsRepository(inner, metrics));
        services.AddMetered<ISavedPlaylistsRepository, SavedPlaylistsRepository>(
            (inner, metrics) => new MeteredSavedPlaylistsRepository(inner, metrics));
        services.AddMetered<ILyricsOffsetsRepository, LyricsOffsetsRepository>(
            (inner, metrics) => new MeteredLyricsOffsetsRepository(inner, metrics));
        services.AddMetered<IRingoGamesRepository, RingoGamesRepository>(
            (inner, metrics) => new MeteredRingoGamesRepository(inner, metrics));
        services.AddMetered<IResistanceGamesRepository, ResistanceGamesRepository>(
            (inner, metrics) => new MeteredResistanceGamesRepository(inner, metrics));
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
}