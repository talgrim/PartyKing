import { axios } from '@/service/request';
import { GameDto, GameSuggestionsDto, RingoGameDto } from './types';

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

  /** Host or admin: the session's Ringo game; rejects with status 404 when there is none. */
  getRingo: (sessionId: string) =>
    axios.get(`/sessions/${sessionId}/games/ringo`, { withCredentials: true }) as unknown as Promise<RingoGameDto>,

  /** Host or admin: starts a Ringo game; rejects with status 409 when the session already has one. */
  startRingo: (sessionId: string) =>
    axios.post(`/sessions/${sessionId}/games/ringo`, null, { withCredentials: true }) as unknown as Promise<RingoGameDto>,

  /** Host or admin: flips a card (0-51); flipping a flipped card changes nothing. */
  flipRingoCard: (sessionId: string, cardId: number) =>
    axios.post(`/sessions/${sessionId}/games/ringo/cards/${cardId}/flip`, null, {
      withCredentials: true,
    }) as unknown as Promise<RingoGameDto>,

  /** Host or admin: ends the session's Ringo game. */
  endRingo: (sessionId: string) =>
    axios.delete(`/sessions/${sessionId}/games/ringo`, { withCredentials: true }) as unknown as Promise<void>,
};
