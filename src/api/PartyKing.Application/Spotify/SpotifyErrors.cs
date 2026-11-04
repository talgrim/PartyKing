using ErrorOr;

namespace PartyKing.Application.Spotify;

internal static class SpotifyErrors
{
    public static Error ProfileNotFound(string profileId)
    {
        return Error.NotFound(description: $"Profile with ID {profileId} was not found in the database");
    }
}
