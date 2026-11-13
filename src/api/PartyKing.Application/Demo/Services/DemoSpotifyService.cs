using ErrorOr;
using PartyKing.Application.Demo.Helpers;
using PartyKing.Application.Spotify.Models;
using PartyKing.Application.Spotify.Services;
using PartyKing.Contract.V1.Spotify;

namespace PartyKing.Application.Demo.Services;

/// <summary>Stands in for <see cref="ISpotifyService"/> in the demo, which has no Spotify: every call is refused.</summary>
internal class DemoSpotifyService : ISpotifyService
{
    public Task<ErrorOr<Uri>> GetHostLoginUrlAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Task.FromResult<ErrorOr<Uri>>(DemoErrors.NotAvailableInDemo("The Spotify login"));
    }

    public Task<ErrorOr<HostLogin>> AuthenticateByCodeAsync(string code, string state, CancellationToken cancellationToken)
    {
        return Task.FromResult<ErrorOr<HostLogin>>(DemoErrors.NotAvailableInDemo("The Spotify login"));
    }

    public Task<ErrorOr<SpotifyAccessGrant>> AuthenticateByProfileIdAsync(string id, CancellationToken cancellationToken)
    {
        return Task.FromResult<ErrorOr<SpotifyAccessGrant>>(DemoErrors.NotAvailableInDemo("The Spotify login"));
    }

    public Task<ErrorOr<IReadOnlyList<SpotifyTrackDto>>> SearchTracksAsync(
        Guid sessionId,
        string query,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<ErrorOr<IReadOnlyList<SpotifyTrackDto>>>(DemoErrors.NotAvailableInDemo("Spotify search"));
    }
}
