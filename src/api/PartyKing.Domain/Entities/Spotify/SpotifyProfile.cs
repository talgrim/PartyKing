using System.Runtime.CompilerServices;

[assembly:InternalsVisibleTo("PartyKing.Persistence")]

namespace PartyKing.Domain.Entities.Spotify;

public class SpotifyProfile
{
    public string Id { get; set; } = null!;
    public string RefreshToken { get; private set; } = null!;
    public string? DisplayName { get; private set; }

    private SpotifyProfile(){}

    public SpotifyProfile(string id, string refreshToken)
    {
        Id = id;
        RefreshToken = refreshToken;
    }

    public void UpdateRefreshToken(string refreshToken)
    {
        RefreshToken = refreshToken;
    }

    public void UpdateDisplayName(string? displayName)
    {
        DisplayName = displayName;
    }

    public static SpotifyProfile Create(string profileId, string refreshToken)
    {
        return new SpotifyProfile(profileId, refreshToken);
    }
}