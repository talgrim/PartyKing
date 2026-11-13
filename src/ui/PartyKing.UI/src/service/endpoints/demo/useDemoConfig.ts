import useSWR from 'swr';
import { demoApi } from './demoApi';

/**
 * Whether this server is a demo. It does not change while the page is open, so it is fetched once. `settled` is false
 * until the first answer (or failure) is in; a server that cannot say is taken for an ordinary one.
 */
export const useDemoConfig = () => {
  const { data, isLoading } = useSWR('demo-config', demoApi.getConfig, {
    shouldRetryOnError: false,
    revalidateOnFocus: false,
    revalidateOnReconnect: false,
    revalidateIfStale: false,
  });

  return { enabled: data?.enabled === true, settled: !isLoading };
};

export const useIsDemo = (): boolean => useDemoConfig().enabled;
