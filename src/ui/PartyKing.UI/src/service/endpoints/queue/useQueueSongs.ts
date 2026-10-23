import useSWR from 'swr';
import { queueApi } from './queueApi';
import { SongDto } from './types';

const QUEUE_REFRESH_MS = 5000;

/** The songs waiting in a session's queue (the one playing is not among them). Shared by every consumer of the key. */
export const useQueueSongs = (sessionId: string) =>
  useSWR<SongDto[]>(['queue-songs', sessionId], () => queueApi.getQueue(sessionId), {
    refreshInterval: QUEUE_REFRESH_MS,
  });
