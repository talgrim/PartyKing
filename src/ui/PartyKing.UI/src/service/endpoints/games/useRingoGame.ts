import useSWR from 'swr';
import { gamesApi } from './gamesApi';

/** The Ringo game of a session; the error has status 404 when the session has none (and 403 for guests). */
export const useRingoGame = (sessionId: string) =>
  useSWR(['ringo', sessionId], () => gamesApi.getRingo(sessionId), { shouldRetryOnError: false });
