// Mirrors PartyKing.Contract.V1.Enums.SongSource, which the API serializes as a number.
export enum SongSource {
  Spotify = 0,
  Youtube = 1,
}

export type SongDto = {
  id: number;
  source: SongSource;
  /** YouTube URL, or Spotify track URI (spotify:track:...) */
  url: string;
  name: string;
  artists: string | null;
  albumArtUrl: string | null;
  durationMs: number | null;
};

export type SpotifyTrackDto = {
  uri: string;
  name: string;
  artists: string;
  albumArtUrl: string | null;
  durationMs: number;
};
