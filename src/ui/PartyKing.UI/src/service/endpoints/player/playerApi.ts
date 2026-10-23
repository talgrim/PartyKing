import { axios } from '@/service/request';
import { NowPlayingDto, PlayerStateRequest, PlayerStateResponse } from './types';

// The axios response interceptor unwraps `response.data`, so these resolve to the payload itself.
// Reports come every ~2 s; one that takes longer is dead (the next one tries again)
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
};
