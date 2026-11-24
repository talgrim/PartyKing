import { axios } from '@/service/request';
import { SongDto } from '../queue/types';
import { SessionDto } from '../sessions/types';

const unwrap = <T>(promise: Promise<unknown>) => promise as Promise<T>;

const withCredentials = { withCredentials: true };

export const adminApi = {
  /** Every session with its host's name. */
  listSessions: () => unwrap<SessionDto[]>(axios.get('/admin/sessions', withCredentials)),

  createSession: (name: string | undefined, password: string) =>
    unwrap<SessionDto>(axios.post('/admin/sessions', { name: name || null, password }, withCredentials)),

  /** Also deletes the session's queue and uploaded photos. */
  deleteSession: (sessionId: string) =>
    unwrap<void>(axios.delete(`/admin/sessions/${sessionId}`, withCredentials)),

  /**
   * Skips to the next song, which the host's player then picks up. Rejects with status 409 when no player is
   * connected and 404 when the queue is empty. `currentSongId` is the song the admin saw playing.
   */
  skipSong: (sessionId: string, currentSongId?: number) =>
    unwrap<SongDto>(
      axios.post(`/admin/sessions/${sessionId}/songs/next`, null, { ...withCredentials, params: { currentSongId } }),
    ),

  /** Calls off the deletion a guest asked for; the session stays. Also succeeds when none was scheduled. */
  cancelSessionDeletion: (sessionId: string) =>
    unwrap<void>(axios.delete(`/admin/sessions/${sessionId}/deletion-request`, withCredentials)),

  /** Removes the host so someone else can claim the session. */
  releaseHost: (sessionId: string) =>
    unwrap<void>(axios.delete(`/admin/sessions/${sessionId}/host`, withCredentials)),
};
