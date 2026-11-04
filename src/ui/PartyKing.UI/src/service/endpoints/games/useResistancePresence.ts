import useSWR from 'swr';
import { resistancePresenceKey } from '@/service/realtime/sessionMusicEvents';
import { gamesApi } from './gamesApi';

/**
 * Who has the Resistance page open, for the TV view (host or admin). The hub pushes every change with the whole list
 * and fetches it again after a reconnect, so there is no polling and no refetch on focus. There is no data until the
 * first answer, and when the server refuses (the viewer is no manager).
 */
export const useResistancePresence = (sessionId: string, enabled: boolean) => {
  const { data } = useSWR(enabled ? resistancePresenceKey(sessionId) : null, () => gamesApi.getResistancePresence(sessionId), {
    shouldRetryOnError: false,
    revalidateOnFocus: false,
  });

  return data;
};
