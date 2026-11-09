import useSWR from 'swr';
import { playlistsApi } from './playlistsApi';

export const savedPlaylistsKey = ['saved-playlists'] as const;

/**
 * The playlists the signed-in account saved, fetched while `enabled` (the settings dialog is open) and asked again
 * every time it opens, because the account may have saved one meanwhile. `playlists` is undefined until the first
 * answer; an `error` with status 401 means nobody is signed in any more.
 */
export const useSavedPlaylists = (enabled: boolean) => {
  const { data, error, isLoading } = useSWR(enabled ? savedPlaylistsKey : null, () => playlistsApi.list(), {
    shouldRetryOnError: false,
    revalidateOnFocus: false,
    dedupingInterval: 0,
  });

  return { playlists: data, error: error as { status?: number; message?: string } | undefined, isLoading };
};
