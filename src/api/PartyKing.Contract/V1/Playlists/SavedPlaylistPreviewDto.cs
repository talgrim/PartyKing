namespace PartyKing.Contract.V1.Playlists;

/// <param name="Sections">Every section with the number of songs it holds now, before de-duplication.</param>
public record SavedPlaylistPreviewDto(IReadOnlyList<PlaylistSectionCountDto> Sections);
