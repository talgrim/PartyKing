import useSWR from 'swr';
import { resistanceKey } from '@/service/realtime/sessionMusicEvents';
import { gamesApi } from './gamesApi';

/**
 * The Resistance game of a session; the error has status 404 when there is none (or it is not open to the viewer yet).
 * It is not fetched again on focus: the hub pushes every change and fetches again after a reconnect, so a tab that
 * wakes up (or is switched to while the other tab of the same player votes) would only add a request.
 */
export const useResistanceGame = (sessionId: string) =>
  useSWR(resistanceKey(sessionId), () => gamesApi.getResistance(sessionId), {
    shouldRetryOnError: false,
    revalidateOnFocus: false,
  });
