import useSWR from 'swr';
import { resistanceKey } from '@/service/realtime/sessionMusicEvents';
import { gamesApi } from './gamesApi';

/** The Resistance lobby of a session; the error has status 404 when there is none (or it is not open to the viewer yet). */
export const useResistanceGame = (sessionId: string) =>
  useSWR(resistanceKey(sessionId), () => gamesApi.getResistance(sessionId), { shouldRetryOnError: false });
