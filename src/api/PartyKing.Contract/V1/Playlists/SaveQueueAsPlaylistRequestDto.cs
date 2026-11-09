using PartyKing.Contract.V1.Enums;

namespace PartyKing.Contract.V1.Playlists;

/// <param name="Name">The name of the new playlist.</param>
/// <param name="Sections">Which groups of the session's songs to save; songs are de-duplicated by their URL.</param>
public record SaveQueueAsPlaylistRequestDto(string? Name, IReadOnlyList<PlaylistSection>? Sections);
