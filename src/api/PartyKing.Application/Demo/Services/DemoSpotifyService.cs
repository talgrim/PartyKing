using ErrorOr;
using PartyKing.Application.Demo.Helpers;
using PartyKing.Application.Ports;
using PartyKing.Application.Spotify;
using PartyKing.Application.Spotify.Models;
using PartyKing.Application.Spotify.Services;
using PartyKing.Contract.V1.Spotify;

namespace PartyKing.Application.Demo.Services;

/// <summary>
/// Stands in for <see cref="ISpotifyService"/> in the demo, which has no Spotify: every call is refused, except that
/// a demo host's session token is a placeholder that no call uses, so the host cookie handler can set it by itself.
/// </summary>
internal class DemoSpotifyService : ISpotifyService
{
    /// <summary>What stands in for the host's access token, which no demo call uses.</summary>
    public const string PlaceholderAccessToken = "demo";

    private const int PlaceholderLifetimeSeconds = 3600;

    private readonly ISpotifyProfilesRepository _profilesRepository;

    public DemoSpotifyService(ISpotifyProfilesRepository profilesRepository)
    {
        _profilesRepository = profilesRepository;
    }

    public Task<ErrorOr<Uri>> GetHostLoginUrlAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Task.FromResult<ErrorOr<Uri>>(DemoErrors.NotAvailableInDemo("The Spotify login"));
    }

    public Task<ErrorOr<HostLogin>> AuthenticateByCodeAsync(string code, string state, CancellationToken cancellationToken)
    {
        return Task.FromResult<ErrorOr<HostLogin>>(DemoErrors.NotAvailableInDemo("The Spotify login"));
    }

    public async Task<ErrorOr<SpotifyAccessGrant>> AuthenticateByProfileIdAsync(string id, CancellationToken cancellationToken)
    {
        // The profile is gone once the demo session was swept
        if (await _profilesRepository.GetProfileAsync(id, cancellationToken) is null)
        {
            return SpotifyErrors.ProfileNotFound(id);
        }

        return new SpotifyAccessGrant(PlaceholderAccessToken, PlaceholderLifetimeSeconds);
    }

    public Task<ErrorOr<IReadOnlyList<SpotifyTrackDto>>> SearchTracksAsync(
        Guid sessionId,
        string query,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<ErrorOr<IReadOnlyList<SpotifyTrackDto>>>(DemoErrors.NotAvailableInDemo("Spotify search"));
    }
}
