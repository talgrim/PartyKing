import { FormEvent, ReactNode, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import useSWR from 'swr';
import { PageNavigation } from '@/components/common/PageNavigation';
import { ROUTE_PATHS, sessionGamesPath, sessionPath } from '@/router/constants';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { adminApi } from '@/service/endpoints/admin/adminApi';
import { ADMIN_HINT_KEY } from '@/service/endpoints/admin/useIsAdmin';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { SessionDto } from '@/service/endpoints/sessions/types';
import { usePersistedFlag } from '@/utils/usePersistedFlag';

const errorMessage = (error: unknown, fallback: string) => (error as { message?: string })?.message || fallback;

const MIN_PASSWORD_LENGTH = 4;

const inputClass = 'w-full px-2 py-2 border-1 border-gray-700 rounded-sm focus:outline-none';
const primaryButton = 'px-3 py-2 bg-amber-600 text-slate-200 rounded-md cursor-pointer disabled:opacity-50';
const secondaryButton = 'px-2 py-1 bg-slate-900 rounded-md cursor-pointer disabled:opacity-50';

export const Admin = (): ReactNode => {
  const { t } = useTranslation();
  // Resolves when the admin cookie is valid, rejects with 401 when the admin has to log in
  const { data: loggedIn, error, mutate: recheck, isLoading } = useSWR('admin-me', () => adminApi.me().then(() => true), {
    shouldRetryOnError: false,
    revalidateOnFocus: false,
  });

  const unauthorized = (error as { status?: number } | undefined)?.status === HTTP_STATUS_CODE.UNAUTHORIZED;

  // Lets Home show the Admin button without asking the API on every visit
  const [, setAdminHint] = usePersistedFlag(ADMIN_HINT_KEY, false);
  useEffect(() => {
    if (loggedIn) {
      setAdminHint(true);
    } else if (unauthorized) {
      setAdminHint(false);
    }
  }, [loggedIn, unauthorized, setAdminHint]);

  return (
    <div className="w-full min-h-full bg-slate-800 text-slate-400">
      <div className="max-w-xl mx-auto pt-6">
        <PageNavigation className="px-2 pt-4" links={[{ to: ROUTE_PATHS.HOME, label: t('admin.allSessions') }]} />
        <h1 className="px-2 pt-4 text-2xl text-slate-200">{t('admin.title')}</h1>

        {isLoading && <p className="px-2">{t('admin.checkingLogin')}</p>}
        {error && !unauthorized && <p className="px-2 text-amber-500">{t('admin.apiUnreachable')}</p>}
        {unauthorized && <LoginForm onLoggedIn={() => recheck(true, { revalidate: false })} />}
        {loggedIn && <Sessions onLoggedOut={() => recheck(undefined, { revalidate: true })} />}
      </div>
    </div>
  );
};

const LoginForm = ({ onLoggedIn }: { onLoggedIn: () => void }): ReactNode => {
  const { t } = useTranslation();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    try {
      await adminApi.login(username, password);
      setPassword('');
      onLoggedIn();
    } catch (error) {
      setMessage(errorMessage(error, t('common.somethingWentWrong')));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="px-2 pt-4 space-y-2" onSubmit={submit}>
      <input
        className={inputClass}
        value={username}
        onChange={(e) => setUsername(e.target.value)}
        placeholder={t('admin.username')}
        autoComplete="username"
        aria-label={t('admin.username')}
      />
      <input
        className={inputClass}
        type="password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        placeholder={t('admin.password')}
        autoComplete="current-password"
        aria-label={t('admin.password')}
      />
      <button type="submit" disabled={busy || !username || !password} className={primaryButton}>
        {t('admin.logIn')}
      </button>
      {message && <p className="text-sm text-amber-500">{message}</p>}
    </form>
  );
};

const Sessions = ({ onLoggedOut }: { onLoggedOut: () => void }): ReactNode => {
  const { t } = useTranslation();
  const [name, setName] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const { data: sessions, mutate } = useSWR('sessions', sessionsApi.getSessions, { refreshInterval: 10000 });

  // Runs an admin action, then refreshes the list; a lost login sends the admin back to the login form
  const run = async (action: () => Promise<unknown>, success?: string) => {
    setBusy(true);
    try {
      await action();
      setMessage(success ?? null);
      await mutate();
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

  const create = async (e: FormEvent) => {
    e.preventDefault();
    await run(() => adminApi.createSession(name.trim(), password), t('admin.sessionCreated'));
    setName('');
    setPassword('');
  };

  const remove = (session: SessionDto) => {
    if (window.confirm(t('admin.confirmDelete', { name: session.name, code: session.code }))) {
      void run(() => adminApi.deleteSession(session.id), t('admin.sessionDeleted'));
    }
  };

  const release = (session: SessionDto) => {
    if (window.confirm(t('admin.confirmRemoveHost', { host: session.hostName, name: session.name }))) {
      void run(() => adminApi.releaseHost(session.id), t('admin.hostRemoved'));
    }
  };

  const logout = async () => {
    await adminApi.logout().catch(() => undefined);
    onLoggedOut();
  };

  const copyLink = async (session: SessionDto) => {
    await navigator.clipboard?.writeText(`${window.location.origin}${sessionPath(session.id)}`);
    setMessage(t('admin.linkCopied', { name: session.name }));
  };

  return (
    <>
      <form className="flex flex-wrap gap-2 mt-4 px-2" onSubmit={create}>
        <input
          className={inputClass}
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder={t('admin.newSessionNamePlaceholder')}
          aria-label={t('admin.newSessionNameLabel')}
          maxLength={60}
        />
        <input
          className={inputClass}
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          placeholder={t('admin.sessionPasswordPlaceholder')}
          autoComplete="new-password"
          aria-label={t('admin.newSessionPasswordLabel')}
          minLength={MIN_PASSWORD_LENGTH}
          maxLength={100}
          required
        />
        <button
          type="submit"
          disabled={busy || password.length < MIN_PASSWORD_LENGTH}
          className={`${primaryButton} shrink-0`}
        >
          {t('admin.createSession')}
        </button>
      </form>

      {message && <p className="px-2 pt-2 text-sm text-amber-500">{message}</p>}

      <ul className="px-2 py-4 space-y-2">
        {(sessions ?? []).map((session) => (
          <li key={session.id} className="p-3 bg-slate-900 rounded-md">
            <div className="flex items-center justify-between gap-2">
              <Link to={sessionPath(session.id)} className="truncate text-lg text-slate-200 hover:underline">
                {session.name}
              </Link>
              <span className="font-mono text-amber-500">{session.code}</span>
            </div>
            <div className="text-sm text-slate-500">
              {session.hasHost ? t('common.hostName', { name: session.hostName }) : t('common.noHost')}
            </div>
            <div className="flex flex-wrap gap-2 mt-2">
              <button className={secondaryButton} disabled={busy} onClick={() => copyLink(session)}>
                {t('admin.copyLink')}
              </button>
              <Link to={sessionGamesPath(session.id)} className={secondaryButton}>
                {t('admin.games')}
              </Link>
              {session.hasHost && (
                <button className={secondaryButton} disabled={busy} onClick={() => release(session)}>
                  {t('admin.removeHost')}
                </button>
              )}
              <button className={`${secondaryButton} text-red-400`} disabled={busy} onClick={() => remove(session)}>
                {t('admin.delete')}
              </button>
            </div>
          </li>
        ))}
        {sessions?.length === 0 && <li>{t('common.noSessionsYet')}</li>}
      </ul>

      <div className="px-2 pb-4">
        <button className={secondaryButton} onClick={logout}>
          {t('admin.logOut')}
        </button>
      </div>
    </>
  );
};
