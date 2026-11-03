using PartyKing.Domain.Entities.Spotify;
using PartyKing.Domain.Results;

namespace PartyKing.Application.Ports;

public interface ISpotifyIntegrationClient
{
    Uri GetAuthenticationUri(string state);
    Task<AuthenticationResult> AuthenticateAsync(string code, CancellationToken cancellationToken);
    Task<ReAuthenticationResult> ReAuthenticateAsync(string refreshToken, CancellationToken cancellationToken);

    Task<IReadOnlyList<Song>> SearchTracksAsync(string accessToken, string query, CancellationToken cancellationToken);
    Task<Song?> GetTrackAsync(string accessToken, string trackId, CancellationToken cancellationToken);

    /// <summary>Null when the playlist does not exist or the token may not read it.</summary>
    Task<Playlist?> GetPlaylistAsync(string accessToken, string playlistId, CancellationToken cancellationToken);

    /// <summary>The playlists of the account the token belongs to, owned and followed alike.</summary>
    Task<IReadOnlyList<Playlist>> GetCurrentUserPlaylistsAsync(string accessToken, CancellationToken cancellationToken);

    /// <summary>The playable tracks of a playlist, in order. Episodes, local files and removed items are left out.</summary>
    Task<IReadOnlyList<Song>> GetPlaylistTracksAsync(
        string accessToken,
        string playlistId,
        CancellationToken cancellationToken);
}
