import { axios } from '@/service/request';
import { GameDto, GameSuggestionsDto } from './types';

// The axios response interceptor unwraps `response.data`, so this resolves to the payload itself.
export const gamesApi = {
  /** Host or admin: the games available in the session; rejects with status 403 for guests. */
  getGames: (sessionId: string) =>
    axios.get(`/sessions/${sessionId}/games`, { withCredentials: true }) as unknown as Promise<GameDto[]>,

  /** Host or admin: spicy "Never have I ever" statements; rejects with status 429 when asked too often. */
  getNeverHaveIEverSuggestions: (sessionId: string) =>
    axios.get(`/sessions/${sessionId}/games/never-have-i-ever/suggestions`, {
      withCredentials: true,
    }) as unknown as Promise<GameSuggestionsDto>,
};
