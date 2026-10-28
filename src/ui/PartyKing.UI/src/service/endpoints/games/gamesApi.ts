import { axios } from '@/service/request';
import { GameDto } from './types';

// The axios response interceptor unwraps `response.data`, so this resolves to the payload itself.
export const gamesApi = {
  /** Host or admin: the games available in the session; rejects with status 403 for guests. */
  getGames: (sessionId: string) =>
    axios.get(`/sessions/${sessionId}/games`, { withCredentials: true }) as unknown as Promise<GameDto[]>,
};
