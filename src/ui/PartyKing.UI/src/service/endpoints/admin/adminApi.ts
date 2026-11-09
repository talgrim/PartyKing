import { axios } from '@/service/request';
import { AccountDto, PagedDto } from '../account/types';
import { SongDto } from '../queue/types';
import { SessionDto } from '../sessions/types';

const unwrap = <T>(promise: Promise<unknown>) => promise as Promise<T>;

const withCredentials = { withCredentials: true };

export const adminApi = {
  /** One page of the registered users (never the admins), ordered by username. `page` is 1-based. */
  listUsers: (params: { search?: string; page: number; pageSize: number }) =>
    unwrap<PagedDto<AccountDto>>(
      axios.get('/admin/users', {
        ...withCredentials,
        params: { search: params.search || undefined, page: params.page, pageSize: params.pageSize },
      }),
    ),

  /** Sets a new password for a user, who is signed out everywhere. Rejects with 403 for an admin, 404 for an unknown user. */
  resetPassword: (accountId: string, newPassword: string) =>
    unwrap<void>(axios.put(`/admin/users/${accountId}/password`, { newPassword }, withCredentials)),

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

  /** Removes the host so someone else can claim the session. */
  releaseHost: (sessionId: string) =>
    unwrap<void>(axios.delete(`/admin/sessions/${sessionId}/host`, withCredentials)),
};
