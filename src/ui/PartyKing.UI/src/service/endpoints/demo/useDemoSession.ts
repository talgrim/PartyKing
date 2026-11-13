import useSWR from 'swr';
import { demoApi } from './demoApi';
import { useIsDemo } from './useDemoConfig';

/**
 * When a demo session ends, or undefined while that is unknown: on a server that is not a demo, before the session is
 * unlocked (`active` false) and for a session that is not a demo one.
 */
export const useDemoSession = (sessionId: string, active: boolean) => {
  const isDemo = useIsDemo();

  const { data } = useSWR(isDemo && active ? ['demo-session', sessionId] : null, () => demoApi.getSession(sessionId), {
    shouldRetryOnError: false,
    revalidateOnFocus: false,
  });

  return data;
};
