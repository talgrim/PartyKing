import { axios } from '@/service/request';
import { SessionDto } from '../sessions/types';

const unwrap = <T>(promise: Promise<unknown>) => promise as Promise<T>;

const withCredentials = { withCredentials: true };

export const adminApi = {
  login: (username: string, password: string) =>
    unwrap<void>(axios.post('/admin/login', { username, password }, withCredentials)),

  logout: () => unwrap<void>(axios.post('/admin/logout', null, withCredentials)),

  /** Resolves when the admin cookie is valid; rejects with status 401 otherwise. */
  me: () => unwrap<void>(axios.get('/admin/me', withCredentials)),

  createSession: (name?: string) =>
    unwrap<SessionDto>(axios.post('/admin/sessions', { name: name || null }, withCredentials)),

  renameSession: (sessionId: string, name: string) =>
    unwrap<void>(axios.patch(`/admin/sessions/${sessionId}`, { name }, withCredentials)),

  /** Also deletes the session's queue. */
  deleteSession: (sessionId: string) =>
    unwrap<void>(axios.delete(`/admin/sessions/${sessionId}`, withCredentials)),

  /** Removes the host so someone else can claim the session. */
  releaseHost: (sessionId: string) =>
    unwrap<void>(axios.delete(`/admin/sessions/${sessionId}/host`, withCredentials)),
};
