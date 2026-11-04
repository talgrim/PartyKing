using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Results;

namespace PartyKing.Application.Ports;

public interface ISpotifyProfilesRepository
{
    Task CreateProfileAsync(AuthenticationResult result, CancellationToken cancellationToken);
    Task<SpotifyProfile?> GetProfileAsync(string id, CancellationToken cancellationToken);
}
