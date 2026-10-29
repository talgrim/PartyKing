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
  /** The timing offset the host or an admin saved for the song; 0 when none was. */
  offsetMs: number;
};

// Mirrors PartyKing.Contract.V1.Lyrics.LyricsOffsetChangedDto
export type LyricsOffsetChangedEvent = {
  /** The queue item that was playing when the offset was saved. */
  songId: number;
  offsetMs: number;
};
