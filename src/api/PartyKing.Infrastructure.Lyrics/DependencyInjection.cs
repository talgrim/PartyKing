using PartyKing.Application.Ports;
using Microsoft.Extensions.DependencyInjection;
using PartyKing.Infrastructure.Lyrics.RestApi;
using RestEase.HttpClientFactory;

namespace PartyKing.Infrastructure.Lyrics;

public static class DependencyInjection
{
    public static void RegisterLyricsInfrastructure(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(ILrclibApi), client =>
        {
            client.BaseAddress = new Uri("https://lrclib.net/");
            client.Timeout = TimeSpan.FromSeconds(5);

            // LRCLIB asks its clients to say who they are
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PartyKing/1.0 (https://party.talgrim.dev)");
        }).UseWithRestEaseClient<ILrclibApi>(restClient =>
            restClient.ResponseDeserializer = new SystemTextJsonResponseDeserializer());

        services.AddTransient<ILrclibClient, LrclibClient>();
    }
}
