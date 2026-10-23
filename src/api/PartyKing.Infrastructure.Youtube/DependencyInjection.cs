using Microsoft.Extensions.DependencyInjection;
using PartyKing.Infrastructure.Youtube.RestApi;

namespace PartyKing.Infrastructure.Youtube;

public static class DependencyInjection
{
    public static void RegisterYoutubeInfrastructure(this IServiceCollection services)
    {
        services.AddHttpClient<IYoutubeOEmbedClient, YoutubeOEmbedClient>(client =>
        {
            client.BaseAddress = new Uri("https://www.youtube.com/");
            client.Timeout = TimeSpan.FromSeconds(5);
        });
    }
}
