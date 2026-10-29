import useSWR from 'swr';
import { lyricsKey } from '@/service/realtime/sessionMusicEvents';
import { lyricsApi } from './lyricsApi';
import { LyricsLineDto } from './types';

export type LyricsState =
  | { status: 'idle' | 'loading' | 'none' }
  | { status: 'found'; songId: number; lines: LyricsLineDto[]; offsetMs: number };

/**
 * The lyrics of the song with `songId`, looked up once per song, with the timing offset saved for it. Only once the
 * session is unlocked (`enabled`). A song without lyrics, or a lookup that failed, is `none`.
 */
export const useLyrics = (sessionId: string, songId: number | null | undefined, enabled: boolean): LyricsState => {
  const { data, isLoading } = useSWR(
    enabled && songId != null ? lyricsKey(sessionId, songId) : null,
    () => lyricsApi.getLyrics(sessionId),
    { shouldRetryOnError: false, revalidateOnFocus: false, revalidateOnReconnect: false },
  );

  if (!enabled || songId == null) return { status: 'idle' };
  if (isLoading) return { status: 'loading' };

  // The server answers for the song playing now; if that moved on meanwhile, these are not this song's lyrics
  return data && data.songId === songId && data.lines.length > 0
    ? { status: 'found', songId, lines: data.lines, offsetMs: data.offsetMs }
    : { status: 'none' };
};
