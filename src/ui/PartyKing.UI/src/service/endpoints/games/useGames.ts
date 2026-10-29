import useSWR from 'swr';
import { gamesApi } from './gamesApi';

/** The games of a session. The server answers only to the host and admins, so guests get an error with status 403. */
export const useGames = (sessionId: string) =>
  useSWR(['games', sessionId], () => gamesApi.getGames(sessionId), { shouldRetryOnError: false });
