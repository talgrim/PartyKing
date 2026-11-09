// Mirrors PartyKing.Contract.V1.Enums.PlaylistSection, which the API serializes as a number.
export enum PlaylistSection {
  History = 0,
  Current = 1,
  Queue = 2,
  Fallback = 3,
}

// Mirrors PartyKing.Contract.V1.Playlists.SavedPlaylistDto
export type SavedPlaylistDto = {
  id: string;
  name: string;
  createdAt: string;
  /** How many songs were saved, after de-duplication. */
  songCount: number;
};

// Mirrors PartyKing.Contract.V1.Playlists.PlaylistSectionCountDto
export type PlaylistSectionCountDto = {
  section: PlaylistSection;
  count: number;
};

// Mirrors PartyKing.Contract.V1.Playlists.SavedPlaylistPreviewDto
export type SavedPlaylistPreviewDto = {
  sections: PlaylistSectionCountDto[];
};
