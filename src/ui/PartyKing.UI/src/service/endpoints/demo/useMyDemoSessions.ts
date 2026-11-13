import useSWR from 'swr';
import { demoApi } from './demoApi';
import { useIsDemo } from './useDemoConfig';

export const MY_DEMO_SESSIONS_KEY = 'demo-my-sessions';

/** The demo sessions this browser started that have not ended; undefined while loading, off a demo and on an error. */
export const useMyDemoSessions = () => {
  const isDemo = useIsDemo();

  return useSWR(isDemo ? MY_DEMO_SESSIONS_KEY : null, demoApi.listMine, { shouldRetryOnError: false });
};
