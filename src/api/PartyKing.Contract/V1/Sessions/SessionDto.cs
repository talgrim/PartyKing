namespace PartyKing.Contract.V1.Sessions;

/// <param name="Code">Short code guests can search for on the landing page.</param>
/// <param name="HostName">Spotify display name of the host; null until the session is claimed.</param>
/// <param name="FallbackPlaylistName">Name of the playlist played when the queue is empty; null when there is none.</param>
/// <param name="FallbackSavedPlaylistId">Id of the saved playlist when that is the fallback; null for a Spotify playlist or none.</param>
/// <param name="DeletionScheduledAt">When the session is deleted because a guest asked for it; null when no deletion is scheduled.</param>
public record SessionDto(
    Guid Id,
    string Code,
    string Name,
    string? HostName,
    bool HasHost,
    string? FallbackPlaylistName = null,
    bool FallbackShuffle = false,
    Guid? FallbackSavedPlaylistId = null,
    DateTimeOffset? DeletionScheduledAt = null);
