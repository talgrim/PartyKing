namespace PartyKing.Contract.V1.Sessions;

/// <param name="PlaylistId">A playlist the signed-in account saved.</param>
/// <param name="Shuffle">Play the playlist in random order instead of in order.</param>
public record SetFallbackSavedPlaylistRequestDto(Guid PlaylistId, bool Shuffle);
