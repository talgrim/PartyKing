import useSWR from 'swr';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { usePersistedFlag } from '@/utils/usePersistedFlag';
import { adminApi } from './adminApi';

/** localStorage hint that this browser had an admin login; the Admin page keeps it up to date. */
export const ADMIN_HINT_KEY = 'partyking.adminHint';

/**
 * Whether the visitor is a logged-in admin, for pages that have no session to ask (`/sessions/{id}/me`).
 * Visitors who never logged in as admin have no hint, so nothing is requested for them. With a hint, the cookie
 * is verified through `GET /admin/me` (same SWR key as the Admin page), and a 401 drops the hint.
 */
export const useIsAdmin = (): boolean => {
  const [hint, setHint] = usePersistedFlag(ADMIN_HINT_KEY, false);

  const { data } = useSWR(hint ? 'admin-me' : null, () => adminApi.me().then(() => true), {
    shouldRetryOnError: false,
    revalidateOnFocus: false,
    onError: (error: { status?: number }) => {
      if (error?.status === HTTP_STATUS_CODE.UNAUTHORIZED) {
        setHint(false);
      }
    },
  });

  return hint && data === true;
};
