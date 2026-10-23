import { axios } from '@/service/request';
import { SessionDto, UserDto } from './types';

// The axios response interceptor unwraps `response.data`, so these resolve to the payload itself.
const unwrap = <T>(promise: Promise<unknown>) => promise as Promise<T>;

export const sessionsApi = {
  getSessions: () => unwrap<SessionDto[]>(axios.get('/sessions')),

  getSession: (sessionId: string) =>
    unwrap<SessionDto>(axios.get(`/sessions/${sessionId}`)),

  /** Spotify login URL that makes the caller the host of the session (or logs the current host in again). */
  createHostLoginUrl: (sessionId: string) =>
    unwrap<string>(axios.post(`/sessions/${sessionId}/host-login-urls`)),

  /** Completes the Spotify login; rejects with status 409 when the session already belongs to someone else. */
  authenticate: (code: string, state: string) =>
    unwrap<UserDto>(
      axios.post('/authenticate', { code, state }, { withCredentials: true }),
    ),
};
