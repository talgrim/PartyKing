using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Configuration;
using PartyKing.Application.Games.Services;
using PartyKing.Application.Lyrics.Services;
using PartyKing.Application.Player.Services;
using PartyKing.Application.Queue.Services;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Application.Spotify.Services;
using PartyKing.Application.System;

namespace PartyKing.Application;

public static class DependencyInjection
{
    public static void RegisterApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.RegisterSystem();
        services.RegisterRealtime();
        services.RegisterSlideshow(configuration);
        services.RegisterSpotify();
        services.RegisterQueue();
        services.RegisterPlayer();
        services.RegisterLyrics();
        services.RegisterGames();
        services.RegisterSessions();
        services.RegisterAdmin(configuration);
    }

    /// <summary>Creates the first admin account from configuration when none exists.</summary>
    public static async Task SeedAdminAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAdminService>().SeedAsync(CancellationToken.None);
    }

    private static void RegisterSessions(this IServiceCollection services)
    {
        services.AddSingleton<IJoinTokenProtector, JoinTokenProtector>();
        services.AddScoped<ISessionService, SessionService>();
    }

    private static void RegisterAdmin(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IAdminService, AdminService>();
        services.Configure<AdminConfiguration>(configuration.GetSection(AdminConfiguration.SectionName));
    }

    private static void RegisterSystem(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
    }

    private static void RegisterRealtime(this IServiceCollection services)
    {
        // The API replaces the notifier with the SignalR implementation; tests keep the no-ops unless they inject a fake
        services.TryAddSingleton<ISessionMusicNotifier, NullSessionMusicNotifier>();
        services.TryAddSingleton<IPlayerConnectionWatchdog, PlayerConnectionWatchdog>();
    }

    private static void RegisterSlideshow(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISlideshowWatchers, SlideshowWatchers>();
        services.AddSingleton<ISlideshowScheduler, SlideshowScheduler>();
        services.AddSingleton<ISlideshowService, SlideshowService>();
        services.Configure<SlideshowConfiguration>(configuration.GetSection(SlideshowConfiguration.SectionName));
    }

    private static void RegisterSpotify(this IServiceCollection services)
    {
        services.AddScoped<ISpotifyService, SpotifyService>();
        services.AddSingleton<IHostTokenProvider, HostTokenProvider>();
    }

    private static void RegisterPlayer(this IServiceCollection services)
    {
        services.AddSingleton<IPlayerStateStore, PlayerStateStore>();
        services.AddScoped<IPlayerService, PlayerService>();
    }

    private static void RegisterLyrics(this IServiceCollection services)
    {
        services.AddSingleton<ILyricsCache, LyricsCache>();
        services.AddScoped<ILyricsService, LyricsService>();
    }

    private static void RegisterGames(this IServiceCollection services)
    {
        services.AddScoped<IGameService, GameService>();
    }

    private static void RegisterQueue(this IServiceCollection services)
    {
        services.AddScoped<IQueueService, QueueService>();
        services.AddScoped<IFallbackQueueFiller, FallbackQueueFiller>();
        services.AddScoped<IFallbackPlaylistService, FallbackPlaylistService>();
        services.AddSingleton<IPlaylistTrackCache, PlaylistTrackCache>();
        services.AddSingleton<IFallbackStatusStore, FallbackStatusStore>();
    }
}