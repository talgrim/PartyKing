import { axios } from '@/service/request';
import { SessionDto, UserDto, ViewerRoleDto } from './types';

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
