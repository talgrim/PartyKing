import useSWR from 'swr';
import { queueApi } from './queueApi';
import { SongDto } from './types';
import { queueSongsKey } from '@/service/realtime/sessionMusicEvents';

/** The songs waiting in a session's queue (the one playing is not among them). Shared by every consumer of the key. Updates arrive over SignalR. */
export const useQueueSongs = (sessionId: string) =>
  useSWR<SongDto[]>(queueSongsKey(sessionId), () => queueApi.getQueue(sessionId));
