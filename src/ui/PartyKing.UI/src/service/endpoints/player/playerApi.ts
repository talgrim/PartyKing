import { axios } from '@/service/request';
import { NowPlayingDto, PlayerCommand, PlayerStateRequest, PlayerStateResponse } from './types';

// The axios response interceptor unwraps `response.data`, so these resolve to the payload itself.
// Reports come every ~10 s (and on every change); one that takes longer is dead (the next one tries again)
const REPORT_TIMEOUT_MS = 8000;

const unwrap = <T>(promise: Promise<unknown>) => promise as Promise<T>;

export const playerApi = {
  getNowPlaying: (sessionId: string) =>
    unwrap<NowPlayingDto>(axios.get(`/sessions/${sessionId}/now-playing`)),

  /** Host only. Tells the server what the player is doing and returns the command an admin sent meanwhile. */
  reportState: (sessionId: string, state: PlayerStateRequest) =>
    unwrap<PlayerStateResponse>(
      axios.post(`/sessions/${sessionId}/player/state`, state, { withCredentials: true, timeout: REPORT_TIMEOUT_MS }),
    ),

  /**
   * Host or admin only. Asks the host's player to pause or resume; rejects with status 403 for anyone else and 409
   * when no player is connected.
   */
  sendCommand: (sessionId: string, command: PlayerCommand) =>
    unwrap<void>(axios.post(`/sessions/${sessionId}/player/commands`, { command }, { withCredentials: true })),
};
