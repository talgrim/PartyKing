import { useCallback, useEffect } from 'react';
import useSWR, { useSWRConfig } from 'swr';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { usePersistedFlag } from '@/utils/usePersistedFlag';
import { accountApi } from './accountApi';

/** localStorage hint that this browser had an account login; `useAccount` keeps it up to date. */
export const ACCOUNT_HINT_KEY = 'partyking.accountHint';

/**
 * The cookie the identity service sets next to its session cookie, on the domain of every site. It carries no
 * identity: it only says "ask", so a browser that logged in on auth.talgrim.dev is noticed here without a visit first.
 */
export const IDENTITY_HINT_COOKIE = 'talgrim-signed-in';

const hasIdentityHintCookie = (): boolean =>
  document.cookie.split(';').some((cookie) => cookie.trim().startsWith(`${IDENTITY_HINT_COOKIE}=`));

const ACCOUNT_KEY = 'account-me';

type Options = {
  /**
   * Ask the API even without a hint. For the pages that exist for signed-in accounts (the admin pages): they must
   * not take a browser that has the cookie but lost the hint (cleared storage) for a visitor.
   */
  alwaysCheck?: boolean;
};

/**
 * Who is signed in (a user or an admin), or null. The accounts belong to the identity service, which is asked through
 * `GET /api/me`. Visitors who never signed in have no hint (neither ours nor the identity service's cookie), so nothing
 * is requested for them (unless `alwaysCheck`). With a hint, the session is verified, and a 401 drops our hint; an
 * account found without a hint sets it. Call `signedOut` after a logout.
 */
export const useAccount = ({ alwaysCheck = false }: Options = {}) => {
  const [hint, setHint] = usePersistedFlag(ACCOUNT_HINT_KEY, false);
  const { mutate } = useSWRConfig();
  const active = hint || alwaysCheck || hasIdentityHintCookie();

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

  const signedOut = useCallback(() => {
    setHint(false);
    void mutate(ACCOUNT_KEY, undefined, { revalidate: false });
  }, [setHint, mutate]);

  return {
    account: active ? (data ?? null) : null,
    isLoading: active && isLoading,
    /** Set for any failure except a 401, which only means nobody is signed in. */
    error: active && (error as { status?: number } | undefined)?.status !== HTTP_STATUS_CODE.UNAUTHORIZED ? error : undefined,
    signedOut,
  };
};

/** Whether the visitor is a signed-in admin. */
export const useIsAdmin = (): boolean => useAccount().account?.role === 'Admin';
