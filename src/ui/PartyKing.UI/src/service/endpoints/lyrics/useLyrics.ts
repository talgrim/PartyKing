import useSWR from 'swr';
import { lyricsApi } from './lyricsApi';
import { LyricsLineDto } from './types';

export type LyricsState =
  | { status: 'idle' | 'loading' | 'none' }
  | { status: 'found'; songId: number; lines: LyricsLineDto[] };

/**
 * The lyrics of the song with `songId`, looked up once per song. Only for the host and admins (`enabled`): the
 * server refuses everyone else. A song without lyrics, or a lookup that failed, is `none`.
 */
export const useLyrics = (sessionId: string, songId: number | null | undefined, enabled: boolean): LyricsState => {
  const { data, isLoading } = useSWR(
    enabled && songId != null ? (['lyrics', sessionId, songId] as const) : null,
    () => lyricsApi.getLyrics(sessionId),
    { shouldRetryOnError: false, revalidateOnFocus: false, revalidateOnReconnect: false },
  );

  if (!enabled || songId == null) return { status: 'idle' };
  if (isLoading) return { status: 'loading' };

  // The server answers for the song playing now; if that moved on meanwhile, these are not this song's lyrics
  return data && data.songId === songId && data.lines.length > 0
    ? { status: 'found', songId, lines: data.lines }
    : { status: 'none' };
};
