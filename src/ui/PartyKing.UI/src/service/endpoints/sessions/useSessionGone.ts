import useSWR from 'swr';
import { sessionsApi } from './sessionsApi';

/**
 * Whether a session this visitor had open no longer exists. The server answers 401 for a deleted session and for one
 * whose password changed alike, so the answer comes from the public list: a session that is no longer listed is gone.
 * Asked only while `enabled` (the session answers 401 after it was open), and false until the list is known.
 */
export const useSessionGone = (sessionId: string, enabled: boolean): boolean => {
  const { data } = useSWR(enabled ? ['session-gone', sessionId] : null, () => sessionsApi.getSessions(), {
    shouldRetryOnError: false,
  });

  return enabled && data !== undefined && !data.some((session) => session.id === sessionId);
};
