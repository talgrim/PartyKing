using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Results;

namespace PartyKing.Application.Ports;

public interface ISpotifyProfilesRepository
{
    Task CreateProfileAsync(AuthenticationResult result, CancellationToken cancellationToken);
    Task<SpotifyProfile?> GetProfileAsync(string id, CancellationToken cancellationToken);

    /// <summary>Forgets the profile (the demo's synthetic hosts); a session it hosted is left without a host.</summary>
    Task DeleteProfileAsync(string id, CancellationToken cancellationToken);
}
