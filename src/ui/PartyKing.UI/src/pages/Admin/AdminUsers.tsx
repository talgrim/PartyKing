import { FormEvent, ReactNode, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import useSWR from 'swr';
import { Dialog, DialogContent, DialogTitle } from '@mui/material';
import {
  MAX_ACCOUNT_PASSWORD_LENGTH,
  MIN_ACCOUNT_PASSWORD_LENGTH,
  errorMessage,
  inputClass,
  primaryButton,
  secondaryButton,
} from '@/components/Account/accountForm';
import { PageNavigation } from '@/components/common/PageNavigation';
import { ROUTE_PATHS } from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { useAccount } from '@/service/endpoints/account/useAccount';
import { AccountDto } from '@/service/endpoints/account/types';
import { adminApi } from '@/service/endpoints/admin/adminApi';

export const USERS_PAGE_SIZE = 20;
const SEARCH_DEBOUNCE_MS = 300;

/** The registered users, a page at a time, with a username search and a password reset for each. Admin only. */
export const AdminUsers = (): ReactNode => {
  const { t } = useTranslation();
  const { account, isLoading, error, signedOut } = useAccount({ alwaysCheck: true });

  return (
    <div className="w-full min-h-full bg-slate-800 text-slate-400">
      <div className="max-w-xl mx-auto pt-6">
        <PageNavigation className="px-2 pt-4" links={[{ to: ROUTE_PATHS.ADMIN, label: t('admin.title') }]} />
        <h1 className="px-2 pt-4 text-2xl text-slate-200">{t('admin.users.title')}</h1>

        {isLoading && <p className="px-2">{t('admin.checkingLogin')}</p>}
        {error && <p className="px-2 text-amber-500">{t('admin.apiUnreachable')}</p>}
        {!isLoading && !error && account?.role !== 'Admin' && (
          <p className="px-2 pt-2">
            {t('admin.users.adminOnly')}{' '}
            <Link to={ROUTE_PATHS.ADMIN} className="text-amber-500 hover:underline">
              {t('admin.users.toAdminPage')}
            </Link>
          </p>
        )}
        {account?.role === 'Admin' && <UserList onLoggedOut={signedOut} />}
      </div>
    </div>
  );
};

const UserList = ({ onLoggedOut }: { onLoggedOut: () => void }): ReactNode => {
  const { t, i18n } = useTranslation();
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(1);
  const [resetting, setResetting] = useState<AccountDto | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  // A new search starts on the first page; typing does not ask the API on every key
  useEffect(() => {
    const timer = setTimeout(() => {
      setQuery(search.trim());
      setPage(1);
    }, SEARCH_DEBOUNCE_MS);

    return () => clearTimeout(timer);
  }, [search]);

  const { data, error } = useSWR(
    ['admin-users', query, page],
    () => adminApi.listUsers({ search: query, page, pageSize: USERS_PAGE_SIZE }),
    {
      keepPreviousData: true,
      shouldRetryOnError: false,
      revalidateOnFocus: false,
      onError: (e: { status?: number }) => {
        if (e?.status === HTTP_STATUS_CODE.UNAUTHORIZED) {
          onLoggedOut();
        }
      },
    },
  );

  const pages = Math.max(Math.ceil((data?.totalCount ?? 0) / USERS_PAGE_SIZE), 1);

  // Users deleted their accounts meanwhile and the page is past the end now
  useEffect(() => {
    if (data && page > pages) {
      setPage(pages);
    }
  }, [data, page, pages]);

  return (
    <>
      <div className="px-2 pt-4">
        <input
          type="search"
          className={inputClass}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('admin.users.searchPlaceholder')}
          aria-label={t('admin.users.searchLabel')}
        />
      </div>

      {message && (
        <p role="status" className="px-2 pt-2 text-sm text-amber-500">
          {message}
        </p>
      )}
      {error && <p className="px-2 pt-2 text-amber-500">{t('admin.users.loadError')}</p>}
      {!data && !error && <p className="px-2 pt-2">{t('admin.users.loading')}</p>}
      {data && data.totalCount === 0 && (
        <p className="px-2 pt-2">{query ? t('admin.users.noMatch', { search: query }) : t('admin.users.none')}</p>
      )}

      {data && data.items.length > 0 && (
        <ul className="px-2 py-4 space-y-2">
          {data.items.map((user) => (
            <li key={user.id} className="flex items-center justify-between gap-2 p-3 bg-slate-900 rounded-md">
              <div className="min-w-0">
                <div className="truncate text-lg text-slate-200">{user.username}</div>
                <div className="text-sm text-slate-500">
                  {t('admin.users.registered', { date: new Date(user.createdAt).toLocaleDateString(i18n.language) })}
                </div>
              </div>
              <button
                type="button"
                className={`${secondaryButton} shrink-0`}
                onClick={() => {
                  setMessage(null);
                  setResetting(user);
                }}
              >
                {t('admin.users.resetPassword')}
              </button>
            </li>
          ))}
        </ul>
      )}

      {data && data.totalCount > 0 && (
        <nav className="flex items-center justify-between gap-2 px-2 pb-4" aria-label={t('admin.users.pagination')}>
          <button type="button" className={secondaryButton} disabled={page <= 1} onClick={() => setPage(page - 1)}>
            {t('admin.users.previous')}
          </button>
          <span className="text-sm">
            {t('admin.users.pageOf', { page, pages })} · {t('admin.users.total', { count: data.totalCount })}
          </span>
          <button type="button" className={secondaryButton} disabled={page >= pages} onClick={() => setPage(page + 1)}>
            {t('admin.users.next')}
          </button>
        </nav>
      )}

      <ResetPasswordDialog
        user={resetting}
        onClose={() => setResetting(null)}
        onLoggedOut={onLoggedOut}
        onReset={(user) => {
          setResetting(null);
          setMessage(t('admin.users.passwordReset', { name: user.username }));
        }}
      />
    </>
  );
};

const ResetPasswordDialog = ({
  user,
  onClose,
  onReset,
  onLoggedOut,
}: {
  user: AccountDto | null;
  onClose: () => void;
  onReset: (user: AccountDto) => void;
  onLoggedOut: () => void;
}): ReactNode => {
  const { t } = useTranslation();
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const close = () => {
    setPassword('');
    setMessage(null);
    onClose();
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!user) return;

    setBusy(true);
    setMessage(null);
    try {
      await adminApi.resetPassword(user.id, password);
      setPassword('');
      onReset(user);
    } catch (error) {
      if ((error as { status?: number })?.status === HTTP_STATUS_CODE.UNAUTHORIZED) {
        onLoggedOut();
        return;
      }
      setMessage(errorMessage(error, t('common.somethingWentWrong')));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={user !== null} onClose={close} aria-labelledby="reset-password-title" fullWidth maxWidth="xs">
      <DialogTitle id="reset-password-title">{t('admin.users.resetTitle', { name: user?.username ?? '' })}</DialogTitle>
      <DialogContent>
        <form className="space-y-2 pt-1" onSubmit={submit}>
          <input
            className={inputClass}
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            placeholder={t('admin.users.newPassword')}
            aria-label={t('admin.users.newPassword')}
            autoComplete="new-password"
            maxLength={MAX_ACCOUNT_PASSWORD_LENGTH}
          />
          <p className="text-sm text-slate-500">{t('account.passwordHint')}</p>
          <div className="flex gap-2">
            <button type="submit" className={primaryButton} disabled={busy || password.length < MIN_ACCOUNT_PASSWORD_LENGTH}>
              {t('admin.users.resetPassword')}
            </button>
            <button type="button" className={secondaryButton} onClick={close}>
              {t('common.cancel')}
            </button>
          </div>
          {message && (
            <p role="alert" className="text-sm text-amber-500">
              {message}
            </p>
          )}
        </form>
      </DialogContent>
    </Dialog>
  );
};
