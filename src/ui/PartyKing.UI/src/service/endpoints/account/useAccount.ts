import { useCallback, useEffect } from 'react';
import useSWR, { useSWRConfig } from 'swr';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { usePersistedFlag } from '@/utils/usePersistedFlag';
import { accountApi } from './accountApi';

/** localStorage hint that this browser had an account login; `useAccount` keeps it up to date. */
export const ACCOUNT_HINT_KEY = 'partyking.accountHint';

const ACCOUNT_KEY = 'account-me';

type Options = {
  /**
   * Ask the API even without a hint. For the pages that exist for signed-in accounts (the admin pages): they must
   * not take a browser that has the cookie but lost the hint (cleared storage) for a visitor.
   */
  alwaysCheck?: boolean;
};

/**
 * Who is signed in (a user or an admin), or null. Visitors who never signed in have no hint, so nothing is requested
 * for them (unless `alwaysCheck`). With a hint, the cookie is verified through `GET /account/me`, and a 401 drops the
 * hint; an account found without a hint sets it. Call `signedIn` after a login or registration and `signedOut`
 * after a logout or a deleted account.
 */
export const useAccount = ({ alwaysCheck = false }: Options = {}) => {
  const [hint, setHint] = usePersistedFlag(ACCOUNT_HINT_KEY, false);
  const { mutate } = useSWRConfig();
  const active = hint || alwaysCheck;

  const { data, error, isLoading } = useSWR(active ? ACCOUNT_KEY : null, accountApi.me, {
    shouldRetryOnError: false,
    revalidateOnFocus: false,
    onError: (e: { status?: number }) => {
      if (e?.status === HTTP_STATUS_CODE.UNAUTHORIZED) {
        setHint(false);
      }
    },
  });

  useEffect(() => {
    if (data && !hint) {
      setHint(true);
    }
  }, [data, hint, setHint]);

  const signedIn = useCallback(() => {
    setHint(true);
    void mutate(ACCOUNT_KEY);
  }, [setHint, mutate]);

  const signedOut = useCallback(() => {
    setHint(false);
    void mutate(ACCOUNT_KEY, undefined, { revalidate: false });
  }, [setHint, mutate]);

  return {
    account: active ? (data ?? null) : null,
    isLoading: active && isLoading,
    /** Set for any failure except a 401, which only means nobody is signed in. */
    error: active && (error as { status?: number } | undefined)?.status !== HTTP_STATUS_CODE.UNAUTHORIZED ? error : undefined,
    signedIn,
    signedOut,
  };
};

/** Whether the visitor is a signed-in admin. */
export const useIsAdmin = (): boolean => useAccount().account?.role === 'Admin';
