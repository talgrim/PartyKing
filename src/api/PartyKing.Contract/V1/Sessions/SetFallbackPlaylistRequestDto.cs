namespace PartyKing.Contract.V1.Sessions;

/// <param name="Playlist">Link or spotify:playlist: URI of a playlist the host owns; null or empty clears it.</param>
/// <param name="Shuffle">Play the playlist in random order instead of in order.</param>
public record SetFallbackPlaylistRequestDto(string? Playlist, bool Shuffle);
