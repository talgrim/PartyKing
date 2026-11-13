using PartyKing.Domain.Results;

namespace PartyKing.Application.Demo.Helpers;

/// <summary>
/// The synthetic Spotify profile a demo session is hosted with: how its id and tokens look, in one place.
/// Only the id, the name and the refresh token are stored; no call ever uses a token.
/// </summary>
internal static class DemoHost
{
    /// <summary>What stands in for the host's access token, which no demo call uses.</summary>
    public const string PlaceholderAccessToken = "demo";

    public const int PlaceholderLifetimeSeconds = 3600;

    private const string DisplayName = "Demo host";
    private const string ProfileIdPrefix = "demo-";
    private const string RefreshTokenPrefix = "demo-refresh-";

    public static string NewProfileId()
    {
        return $"{ProfileIdPrefix}{Guid.NewGuid():N}";
    }

    /// <summary>The profile to store for <paramref name="profileId"/>; the refresh token is unique to it.</summary>
    public static AuthenticationResult NewProfile(string profileId)
    {
        return new AuthenticationResult(
            profileId,
            DisplayName,
            PlaceholderAccessToken,
            $"{RefreshTokenPrefix}{Guid.NewGuid():N}",
            PlaceholderLifetimeSeconds);
    }
}
