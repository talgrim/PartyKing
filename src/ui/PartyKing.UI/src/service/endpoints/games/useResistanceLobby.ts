import useSWR from 'swr';
import { resistanceLobbyKey } from '@/service/realtime/sessionMusicEvents';
import { gamesApi } from './gamesApi';

/**
 * Whether the session's Resistance game is open to join and whether the viewer did. There is no data (404) without a game:
 * SWR keeps the last data after a failed refetch, so a game that was ended is dropped explicitly.
 */
export const useResistanceLobby = (sessionId: string, enabled = true) => {
  const { data, error, ...rest } = useSWR(enabled ? resistanceLobbyKey(sessionId) : null, () => gamesApi.getResistanceLobby(sessionId), {
    shouldRetryOnError: false,
  });

  return { ...rest, data: error ? undefined : data, error };
};
