// Mirrors PartyKing.Contract.V1.Lyrics.LyricsLineDto
export type LyricsLineDto = {
  /** When the line starts being sung, from the start of the song. */
  timeMs: number;
  text: string;
};

// Mirrors PartyKing.Contract.V1.Lyrics.LyricsDto
export type LyricsDto = {
  /** The queue item the lyrics belong to. */
  songId: number;
  /** Ordered by time. */
  lines: LyricsLineDto[];
};
