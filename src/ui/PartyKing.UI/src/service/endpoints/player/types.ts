import { SongDto } from '../queue/types';

// Mirrors PartyKing.Contract.V1.Player.NowPlayingDto
export type NowPlayingDto = {
  /** The song being played; null when nothing is. */
  song: SongDto | null;
  /** The host's player reported that it is paused. */
  paused: boolean;
  /** The host's player reported in recently, i.e. someone is listening. */
  playerConnected: boolean;
  /** Where in the song the player is as of this response (the server counts on while it plays); 0 when unknown. */
  positionMs?: number;
};

// Mirrors PartyKing.Contract.V1.Enums.PlayerCommand, which the API serializes as a string.
export type PlayerCommand = 'Pause' | 'Resume';

export type PlayerStateRequest = {
  /** The song the player is playing; null when it is idle. */
  songId: number | null;
  paused: boolean;
  /** Where in the song the player is. */
  positionMs: number;
  /** The player jumped to another position since its previous report. */
  seeked: boolean;
};

export type PlayerStateResponse = {
  /** A command an admin sent since the last report. */
  command: PlayerCommand | null;
  /** Why the fallback playlist could not add a song; null when it works or is not set. */
  fallbackError?: string | null;
};
