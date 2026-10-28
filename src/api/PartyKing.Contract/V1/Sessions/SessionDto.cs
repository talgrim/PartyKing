namespace PartyKing.Contract.V1.Sessions;

/// <param name="Code">Short code guests can search for on the landing page.</param>
/// <param name="HostName">Spotify display name of the host; null until the session is claimed.</param>
/// <param name="FallbackPlaylistName">Name of the playlist played when the queue is empty; null when there is none.</param>
public record SessionDto(
    Guid Id,
    string Code,
    string Name,
    string? HostName,
    bool HasHost,
    string? FallbackPlaylistName = null,
    bool FallbackShuffle = false);
