import useSWR from 'swr';
import { playerApi } from './playerApi';

const NOW_PLAYING_REFRESH_MS = 4000;

/** The song playing in a session, whether it is paused and whether a player is connected. */
export const useNowPlaying = (sessionId: string) =>
  useSWR(['now-playing', sessionId], () => playerApi.getNowPlaying(sessionId), {
    refreshInterval: NOW_PLAYING_REFRESH_MS,
  });
