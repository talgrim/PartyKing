import { axios } from '@/service/request';
import { JoinTokenDto, PlaylistDto, SessionDto, SlideshowSettingsDto, UserDto, ViewerRoleDto } from './types';

// The axios response interceptor unwraps `response.data`, so these resolve to the payload itself.
const unwrap = <T>(promise: Promise<unknown>) => promise as Promise<T>;

export const sessionsApi = {
  getSessions: () => unwrap<SessionDto[]>(axios.get('/sessions')),

  getSession: (sessionId: string) =>
    unwrap<SessionDto>(axios.get(`/sessions/${sessionId}`)),

  /**
   * Checks the session password and unlocks the session for this browser (a cookie). Rejects with status 401 for a
   * wrong password, and also for an unknown session.
   */
  join: (sessionId: string, password: string) =>
    unwrap<void>(axios.post(`/sessions/${sessionId}/join`, { password }, { withCredentials: true })),

  /**
   * Unlocks the session with the token from a QR code. Rejects with status 401 when the token is invalid, expired
   * or outdated (the password changed), and 429 when tried too often.
   */
  joinWithToken: (sessionId: string, token: string) =>
    unwrap<void>(axios.post(`/sessions/${sessionId}/join-with-token`, { token }, { withCredentials: true })),

  /** Creates a short-lived token for a QR code. Host or admin only; rejects with status 403 for anyone else. */
  createJoinToken: (sessionId: string) =>
    unwrap<JoinTokenDto>(axios.post(`/sessions/${sessionId}/join-token`, undefined, { withCredentials: true })),

  /** Host or admin only; rejects with status 403 for anyone else, 400 for an empty or too long name. */
  renameSession: (sessionId: string, name: string) =>
    unwrap<void>(axios.patch(`/sessions/${sessionId}`, { name }, { withCredentials: true })),

  /**
   * Host or admin only. Everyone else who had unlocked the session has to enter the new password; the caller
   * stays unlocked. Rejects with status 403 for anyone else, 400 for an invalid password.
   */
  changePassword: (sessionId: string, password: string) =>
    unwrap<void>(axios.put(`/sessions/${sessionId}/password`, { password }, { withCredentials: true })),

  /** Host or admin only; rejects with status 403 for anyone else. */
  getSlideshowSettings: (sessionId: string) =>
    unwrap<SlideshowSettingsDto>(axios.get(`/sessions/${sessionId}/slideshow-settings`, { withCredentials: true })),

  updateSlideshowSettings: (sessionId: string, settings: SlideshowSettingsDto) =>
    unwrap<void>(axios.put(`/sessions/${sessionId}/slideshow-settings`, settings, { withCredentials: true })),

  /**
   * Host or admin only. Sets the playlist (link or spotify:playlist: URI) played when the queue is empty, or clears
   * it when `playlist` is null. Rejects with 400 for a playlist the host does not own, 403 for anyone else and 409
   * when the session has no host.
   */
  setFallbackPlaylist: (sessionId: string, playlist: string | null, shuffle: boolean) =>
    unwrap<void>(axios.put(`/sessions/${sessionId}/fallback-playlist`, { playlist, shuffle }, { withCredentials: true })),

  /** The playlists the logged-in host owns. Rejects with 403 for anyone but the host, 409 when the host must log in again. */
  getHostPlaylists: (sessionId: string) =>
    unwrap<PlaylistDto[]>(axios.get(`/sessions/${sessionId}/host-playlists`, { withCredentials: true })),

  /** What the logged-in visitor (host or admin, if any) may do in the session. */
  getViewerRole: (sessionId: string) =>
    unwrap<ViewerRoleDto>(axios.get(`/sessions/${sessionId}/me`, { withCredentials: true })),

  /**
   * Uploads photos to the session's slideshow. Rejects with status 400 (the message says why), 401 (session locked),
   * 413 (too big) or 429 (too many uploads).
   */
  uploadPhotos: (
    sessionId: string,
    files: File[],
    deleteAfterPresentation: boolean,
    onProgress?: (percent: number) => void,
  ) => {
    const formData = new FormData();
    files.forEach((file) => formData.append('files', file));

    return unwrap<void>(
      axios.put(`/sessions/${sessionId}/photos/${deleteAfterPresentation}`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
        onUploadProgress: (event) => {
          if (event.total) onProgress?.(Math.round((event.loaded * 100) / event.total));
        },
      }),
    );
  },

  /** Spotify login URL that makes the caller the host of the session (or logs the current host in again). */
  createHostLoginUrl: (sessionId: string) =>
    unwrap<string>(axios.post(`/sessions/${sessionId}/host-login-urls`)),

  /** Completes the Spotify login; rejects with status 409 when the session already belongs to someone else. */
  authenticate: (code: string, state: string) =>
    unwrap<UserDto>(
      axios.post('/authenticate', { code, state }, { withCredentials: true }),
    ),
};
