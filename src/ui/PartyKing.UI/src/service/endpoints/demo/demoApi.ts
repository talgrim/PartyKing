import { axios } from '@/service/request';
import { DemoConfigDto, DemoSessionDto, DemoStartedDto } from './types';

// The axios response interceptor unwraps `response.data`, so these resolve to the payload itself.
const unwrap = <T>(promise: Promise<unknown>) => promise as Promise<T>;

export const demoApi = {
  getConfig: () => unwrap<DemoConfigDto>(axios.get('/demo/config')),

  /**
   * Starts a session of the visitor's own and makes them its host (the cookies come with the answer).
   * Rejects with status 404 on a server that is not a demo, 409 while the demo is full and 429 after too many starts.
   */
  start: () => unwrap<DemoStartedDto>(axios.post('/demo/sessions')),

  /** When the session ends. Rejects with status 404 for a session that is not a demo one, 401 until it is unlocked. */
  getSession: (sessionId: string) => unwrap<DemoSessionDto>(axios.get(`/sessions/${sessionId}/demo`)),
};
