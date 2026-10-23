using SpotifyAPI.Web;
using SpotifyAPI.Web.Http;

namespace PartyKing.Infrastructure.Spotify.RestApi;

public interface ISpotifyClientFactory
{
    ISpotifyClient CreateClient(string accessToken);
    IOAuthClient CreateOAuthClient();
}

internal class SpotifyClientFactory : ISpotifyClientFactory
{
    public const string HttpClientName = "spotify";

    private readonly IHttpClientFactory _httpClientFactory;

    public SpotifyClientFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public ISpotifyClient CreateClient(string accessToken)
    {
        return new SpotifyClient(CreateConfig().WithToken(accessToken));
    }

    public IOAuthClient CreateOAuthClient()
    {
        return new OAuthClient(CreateConfig());
    }

    // The HttpClient comes from the factory, which pools and recycles the handlers behind it
    private SpotifyClientConfig CreateConfig()
    {
        return SpotifyClientConfig.CreateDefault()
            .WithHTTPClient(new NetHttpClient(_httpClientFactory.CreateClient(HttpClientName)));
    }
}
