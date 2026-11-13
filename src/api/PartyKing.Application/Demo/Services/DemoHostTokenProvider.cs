using ErrorOr;
using PartyKing.Application.Demo.Helpers;
using PartyKing.Application.Spotify.Services;

namespace PartyKing.Application.Demo.Services;

/// <summary>
/// Stands in for <see cref="IHostTokenProvider"/> in the demo: the synthetic hosts have no Spotify account, so everything
/// that needs the host's token (Spotify songs, search, Spotify playlists) is refused instead of reaching Spotify.
/// </summary>
internal class DemoHostTokenProvider : IHostTokenProvider
{
    public Task<ErrorOr<string>> GetSessionAccessTokenAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return Task.FromResult<ErrorOr<string>>(DemoErrors.NotAvailableInDemo("Spotify"));
    }

    public Task<ErrorOr<string>> GetAccessTokenAsync(string profileId, CancellationToken cancellationToken)
    {
        return Task.FromResult<ErrorOr<string>>(DemoErrors.NotAvailableInDemo("Spotify"));
    }

    public void Invalidate(string profileId)
    {
    }
}
