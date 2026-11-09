import { axios } from '@/service/request';
import { PlaylistSection, SavedPlaylistDto, SavedPlaylistPreviewDto } from './types';

// The axios response interceptor unwraps `response.data`, so these resolve to the payload itself.
const unwrap = <T>(promise: Promise<unknown>) => promise as Promise<T>;

const withCredentials = { withCredentials: true };

export const playlistsApi = {
  /**
   * Signed-in account only. Saves the chosen parts of the session's queue as a new playlist of the account. Rejects
   * with status 400 for a blank or too long name, nothing to save, or an account at its limit; 401 when nobody is
   * signed in (or the login expired).
   */
  saveQueue: (sessionId: string, name: string, sections: PlaylistSection[]) =>
    unwrap<SavedPlaylistDto>(
      axios.post(`/sessions/${sessionId}/saved-playlists`, { name, sections }, withCredentials),
    ),

  /** Signed-in account only. The playlists the account saved, the newest first; rejects with 401 like `saveQueue`. */
  list: () => unwrap<SavedPlaylistDto[]>(axios.get('/account/saved-playlists', withCredentials)),

  /** Signed-in account only. How many songs each part of the session's queue holds now; rejects with 401 like `saveQueue`. */
  getPreview: (sessionId: string) =>
    unwrap<SavedPlaylistPreviewDto>(axios.get(`/sessions/${sessionId}/saved-playlists/preview`, withCredentials)),
};
