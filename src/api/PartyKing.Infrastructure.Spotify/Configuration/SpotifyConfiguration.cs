namespace PartyKing.Application.Configuration;

public class SpotifyConfiguration
{
    public const string SectionName = "SpotifySettings";
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
    public Uri RedirectUri { get; set; } = null!;
}