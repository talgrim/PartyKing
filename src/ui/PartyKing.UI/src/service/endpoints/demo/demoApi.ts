import { axios } from '@/service/request';
import { DemoConfigDto, DemoSessionDto, DemoStartedDto, MyDemoSessionsDto } from './types';

// The axios response interceptor unwraps `response.data`, so these resolve to the payload itself.
const unwrap = <T>(promise: Promise<unknown>) => promise as Promise<T>;

export const demoApi = {
  getConfig: () => unwrap<DemoConfigDto>(axios.get('/demo/config')),

  /**
   * Starts a session of the visitor's own and makes them its host (the cookies come with the answer).
   * Rejects with status 404 on a server that is not a demo, 409 while the demo is full and 429 after too many starts.
   */
  start: () => unwrap<DemoStartedDto>(axios.post('/demo/sessions')),

  /** The demo sessions this browser started that have not ended. Rejects with status 404 on a server that is not a demo. */
  listMine: () => unwrap<MyDemoSessionsDto>(axios.get('/demo/sessions/mine')),

  /**
   * Makes the visitor the host of one of their own demo sessions again (the cookies come with the answer). Rejects
   * with status 404 for a session this browser did not start and one that has ended.
   */
  resume: (sessionId: string) => unwrap<DemoStartedDto>(axios.post(`/demo/sessions/${sessionId}/resume`)),

  /** When the session ends. Rejects with status 404 for a session that is not a demo one, 401 until it is unlocked. */
  getSession: (sessionId: string) => unwrap<DemoSessionDto>(axios.get(`/sessions/${sessionId}/demo`)),
};
