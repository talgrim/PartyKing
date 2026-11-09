import useSWR from 'swr';
import { playlistsApi } from './playlistsApi';
import { PlaylistSection } from './types';

export const playlistPreviewKey = (sessionId: string) => ['saved-playlist-preview', sessionId] as const;

/**
 * How many songs each section of the session's queue holds, fetched while `enabled` (the dialog is open) and asked
 * again every time it opens, because the queue moves on. `counts` is undefined until the first answer.
 */
export const usePlaylistPreview = (sessionId: string, enabled: boolean) => {
  const { data, error, isLoading } = useSWR(enabled ? playlistPreviewKey(sessionId) : null, () =>
    playlistsApi.getPreview(sessionId),
    { shouldRetryOnError: false, revalidateOnFocus: false, dedupingInterval: 0 },
  );

  const counts = data && new Map<PlaylistSection, number>(data.sections.map((x) => [x.section, x.count]));

  return { counts, error: error as { status?: number; message?: string } | undefined, isLoading };
};
