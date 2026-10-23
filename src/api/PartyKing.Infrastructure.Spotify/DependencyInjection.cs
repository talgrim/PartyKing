using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Application.Configuration;
using PartyKing.Infrastructure.Spotify.Metrics;
using PartyKing.Infrastructure.Spotify.RestApi;

namespace PartyKing.Infrastructure.Spotify;

public static class DependencyInjection
{
    public static void RegisterSpotifyInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SpotifyConfiguration>(configuration.GetSection(SpotifyConfiguration.SectionName));
        services.AddHttpClient(SpotifyClientFactory.HttpClientName);
        services.AddSingleton<ISpotifyClientFactory, SpotifyClientFactory>();
        services.AddSingleton<SpotifyClientMetrics>();
        services.AddScoped<SpotifyIntegrationClient>();
        services.AddScoped<ISpotifyIntegrationClient>(provider => new MeteredSpotifyIntegrationClient(
            provider.GetRequiredService<SpotifyIntegrationClient>(),
            provider.GetRequiredService<SpotifyClientMetrics>()));
    }
}