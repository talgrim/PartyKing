import useSWR from 'swr';
import { ringoKey } from '@/service/realtime/sessionMusicEvents';
import { gamesApi } from './gamesApi';

/** The Ringo game of a session; the error has status 404 when the session has none (and 403 for guests). */
export const useRingoGame = (sessionId: string) =>
  useSWR(ringoKey(sessionId), () => gamesApi.getRingo(sessionId), { shouldRetryOnError: false });
