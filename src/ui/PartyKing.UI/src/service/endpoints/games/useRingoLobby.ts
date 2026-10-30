import useSWR from 'swr';
import { ringoLobbyKey } from '@/service/realtime/sessionMusicEvents';
import { gamesApi } from './gamesApi';

/**
 * Whether the session's Ringo game is open to join and whether the viewer did. There is no data (404) without a game:
 * SWR keeps the last data after a failed refetch, so a game that was ended is dropped explicitly.
 */
export const useRingoLobby = (sessionId: string, enabled = true) => {
  const { data, error, ...rest } = useSWR(enabled ? ringoLobbyKey(sessionId) : null, () => gamesApi.getRingoLobby(sessionId), {
    shouldRetryOnError: false,
  });

  return { ...rest, data: error ? undefined : data, error };
};
