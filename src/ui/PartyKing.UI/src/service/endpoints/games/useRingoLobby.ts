import useSWR from 'swr';
import { ringoLobbyKey } from '@/service/realtime/sessionMusicEvents';
import { gamesApi } from './gamesApi';

/** Whether the session's Ringo game is open to join and whether the viewer did; no data (404) without a game. */
export const useRingoLobby = (sessionId: string, enabled = true) =>
  useSWR(enabled ? ringoLobbyKey(sessionId) : null, () => gamesApi.getRingoLobby(sessionId), {
    shouldRetryOnError: false,
  });
