using SpotifyAPI.Web;

namespace PartyKing.Infrastructure.Spotify.RestApi;

public interface ISpotifyClientFactory
{
    ISpotifyClient CreateClient(string accessToken);
}

public class SpotifyClientFactory : ISpotifyClientFactory
{
    public ISpotifyClient CreateClient(string accessToken)
    {
        return new SpotifyClient(accessToken);
    }
}