using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PartyKing.Application.Admin.Services;
using PartyKing.Application.Configuration;
using PartyKing.Application.Events;
using PartyKing.Application.Games.Handlers;
using PartyKing.Application.Games.Services.Base;
using PartyKing.Application.Games.Services.NeverHaveIEver;
using PartyKing.Application.Games.Services.Resistance;
using PartyKing.Application.Games.Services.Ringo;
using PartyKing.Application.Lyrics.Services;
using PartyKing.Application.Player.Services;
using PartyKing.Application.Queue.Services;
using PartyKing.Application.Realtime;
using PartyKing.Application.Sessions.Services;
using PartyKing.Application.Slideshow.Services;
using PartyKing.Application.Spotify.Services;
using PartyKing.Application.System;
using PartyKing.Domain.Events;
using PartyKing.Domain.Events.Games.Base;
using PartyKing.Domain.Events.Games.Ringo;

namespace PartyKing.Application;

public static class DependencyInjection
{
    public static void RegisterApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.RegisterSystem();
        services.RegisterRealtime();
        services.RegisterDomainEvents();
        services.RegisterSlideshow(configuration);
        services.RegisterSpotify();
        services.RegisterQueue();
        services.RegisterPlayer();
        services.RegisterLyrics();
        services.RegisterGames(configuration);
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
        services.AddScoped<ISessionAuthorizer, SessionAuthorizer>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<ISessionAdminService, SessionAdminService>();
        services.AddSingleton<SessionDummyHash>();
        services.AddScoped<ISessionAccessService, SessionAccessService>();
        services.AddScoped<ISessionJoinTokenService, SessionJoinTokenService>();
        services.AddScoped<IHostSessionCleanup, HostSessionCleanup>();
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

    private static void RegisterDomainEvents(this IServiceCollection services)
    {
        services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
    }

    /// <summary>Registers a handler of <typeparamref name="TEvent"/>; an event may have any number of them.</summary>
    private static void AddDomainEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IDomainEvent
        where THandler : class, IDomainEventHandler<TEvent>
    {
        services.AddScoped<IDomainEventHandler<TEvent>, THandler>();
    }

    private static void RegisterRealtime(this IServiceCollection services)
    {
        // The API replaces the notifiers with the SignalR implementations; tests keep the no-ops unless they inject a fake
        services.TryAddSingleton<IQueueNotifier, NullQueueNotifier>();
        services.TryAddSingleton<IPlayerNotifier, NullPlayerNotifier>();
        services.TryAddSingleton<ILyricsNotifier, NullLyricsNotifier>();
        services.TryAddSingleton<ISlideshowNotifier, NullSlideshowNotifier>();
        services.TryAddSingleton<IRingoNotifier, NullRingoNotifier>();
        services.TryAddSingleton<IResistanceNotifier, NullResistanceNotifier>();
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

    private static void RegisterGames(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IGameService, GameService>();
        services.AddDomainEventHandler<LobbyOpened, LobbyPushHandler>();
        services.AddDomainEventHandler<LobbyPlayerJoined, LobbyPushHandler>();
        services.AddDomainEventHandler<LobbyPlayerKicked, LobbyPushHandler>();
        services.AddDomainEventHandler<LobbyOrderChanged, LobbyPushHandler>();
        services.AddDomainEventHandler<RingoCardFlipped, RingoPushHandler>();
        services.AddDomainEventHandler<RingoTurnPassed, RingoPushHandler>();
        services.AddScoped<RingoGameLobby>();
        services.AddScoped<IRingoService, RingoService>();
        services.AddScoped<ResistanceGameLobby>();
        services.AddScoped<ResistanceActionGate>();
        services.AddScoped<IResistanceLobbyService, ResistanceLobbyService>();
        services.AddScoped<IResistanceRoundService, ResistanceRoundService>();
        services.AddScoped<IResistancePlotCardService, ResistancePlotCardService>();
        services.AddSingleton<IResistanceRulesProvider, ResistanceRulesProvider>();
        services.AddSingleton<IValidateOptions<ResistanceConfiguration>, ResistanceConfigurationValidator>();
        services.Configure<ResistanceConfiguration>(configuration.GetSection(ResistanceConfiguration.SectionName));
        services.AddScoped<INeverHaveIEverSuggestionService, NeverHaveIEverSuggestionService>();
        services.AddScoped<IRingoRuleSuggestionService, RingoRuleSuggestionService>();
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