using System.Runtime.CompilerServices;

[assembly:InternalsVisibleTo("PartyKing.Persistence")]

namespace PartyKing.Domain.Entities;

public class SpotifyProfile
{
    public int Id { get; set; }
    public string RefreshToken { get; internal init; } = null!;

    private SpotifyProfile(){}

    public SpotifyProfile(int id, string refreshToken)
    {
        Id = id;
        RefreshToken = refreshToken;
    }

    public static SpotifyProfile Create(int profileId, string refreshToken)
    {
        return new SpotifyProfile(profileId, refreshToken);
    }
}