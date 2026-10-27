import { SongDto } from '@/service/endpoints/queue/types';
import { NowPlayingDto } from '@/service/endpoints/player/types';

export const SESSION_MUSIC_EVENTS = {
  SongAdded: 'SongAdded',
  SongRemoved: 'SongRemoved',
  QueueAdvanced: 'QueueAdvanced',
  PlayerStateChanged: 'PlayerStateChanged',
} as const;

export type SongRemovedEvent = { songId: number };

export type QueueAdvancedEvent = {
  previousSongId: number | null;
  nowPlaying: SongDto | null;
};

export type PlayerStateChangedEvent = {
  songId: number | null;
  paused: boolean;
  playerConnected: boolean;
};

export const queueSongsKey = (sessionId: string) => ['queue-songs', sessionId] as const;
export const nowPlayingKey = (sessionId: string) => ['now-playing', sessionId] as const;

export const applySongAdded = (queue: SongDto[] | undefined, song: SongDto): SongDto[] => {
  if (!queue) return [song];
  return queue.some((item) => item.id === song.id) ? queue : [...queue, song];
};

export const applySongRemoved = (queue: SongDto[] | undefined, songId: number): SongDto[] | undefined =>
  queue?.filter((item) => item.id !== songId);

export const applyQueueAdvancedToQueue = (
  queue: SongDto[] | undefined,
  event: QueueAdvancedEvent,
): SongDto[] | undefined => {
  if (!queue) return queue;
  const dropIds = new Set(
    [event.previousSongId, event.nowPlaying?.id].filter((id): id is number => id != null),
  );
  return queue.filter((item) => !dropIds.has(item.id));
};

export const applyQueueAdvancedToNowPlaying = (
  current: NowPlayingDto | undefined,
  event: QueueAdvancedEvent,
): NowPlayingDto => ({
  song: event.nowPlaying,
  paused: false,
  playerConnected: current?.playerConnected ?? false,
});

export const applyPlayerStateChanged = (
  current: NowPlayingDto | undefined,
  event: PlayerStateChangedEvent,
): NowPlayingDto => {
  const song = current?.song ?? null;
  const paused = song != null && event.songId === song.id && event.paused;
  return {
    song,
    paused,
    playerConnected: event.playerConnected,
  };
};
