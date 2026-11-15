import { useState } from 'react';
import useSWR from 'swr';
import { demoApi } from './demoApi';

const RETRY_INTERVAL_MS = 1500;
const MAX_RETRIES = 20;
const GATEWAY_STATUSES = [502, 503, 504];

/**
 * Whether the server has not answered yet, as opposed to answering "no" (a 404 on a server that is not a demo). A
 * suspended Fly machine starts nginx before the API, so the first calls end as a 502 or without any answer.
 */
const isServerNotUp = (error: { status?: number } | undefined): boolean =>
  !!error && (error.status === undefined || GATEWAY_STATUSES.includes(error.status));

/**
 * Whether this server is a demo. It does not change while the page is open, so it is fetched once. `settled` is false
 * until the first answer is in. A server that is not up yet is asked again (every 1.5 s, 20 times) rather than taken
 * for an ordinary one; a server that answers with an error, or never comes up, is taken for an ordinary one.
 */
export const useDemoConfig = () => {
  const [gaveUp, setGaveUp] = useState(false);

  const { data, error, isLoading } = useSWR('demo-config', demoApi.getConfig, {
    onErrorRetry: (error, _key, _config, revalidate, { retryCount }) => {
      if (!isServerNotUp(error)) return;

      if (retryCount > MAX_RETRIES) {
        setGaveUp(true);
        return;
      }

      setTimeout(() => revalidate({ retryCount }), RETRY_INTERVAL_MS);
    },
    revalidateOnFocus: false,
    revalidateOnReconnect: false,
    revalidateIfStale: false,
  });

  const waiting = isServerNotUp(error) && !gaveUp;

  return { enabled: data?.enabled === true, settled: !isLoading && !waiting, waiting };
};

export const useIsDemo = (): boolean => useDemoConfig().enabled;
