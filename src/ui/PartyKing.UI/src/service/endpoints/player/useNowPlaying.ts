import useSWR from 'swr';
import { playerApi } from './playerApi';
import { nowPlayingKey } from '@/service/realtime/sessionMusicEvents';

/** The song playing in a session, whether it is paused and whether a player is connected. Updates arrive over SignalR. */
export const useNowPlaying = (sessionId: string) =>
  useSWR(nowPlayingKey(sessionId), () => playerApi.getNowPlaying(sessionId));
