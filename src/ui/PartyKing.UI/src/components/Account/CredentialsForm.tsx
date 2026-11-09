import { FormEvent, ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { accountApi } from '@/service/endpoints/account/accountApi';
import {
  MAX_ACCOUNT_PASSWORD_LENGTH,
  MAX_ACCOUNT_USERNAME_LENGTH,
  MIN_ACCOUNT_PASSWORD_LENGTH,
  errorMessage,
  inputClass,
  primaryButton,
} from './accountForm';

type Props = {
  mode: 'login' | 'register';
  /** Called once the server signed the visitor in (the cookie is set). */
  onDone: () => void;
};

/** Username and password: logs in with an existing account, or makes a new one and signs it in. */
export const CredentialsForm = ({ mode, onDone }: Props): ReactNode => {
  const { t } = useTranslation();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const registering = mode === 'register';

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setMessage(null);
    try {
      await (registering ? accountApi.register : accountApi.login)(username, password);
      setPassword('');
      onDone();
    } catch (error) {
      setMessage(errorMessage(error, t('common.somethingWentWrong')));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="space-y-2" onSubmit={submit}>
      <input
        className={inputClass}
        value={username}
        onChange={(e) => setUsername(e.target.value)}
        placeholder={t('account.username')}
        autoComplete="username"
        aria-label={t('account.username')}
        maxLength={MAX_ACCOUNT_USERNAME_LENGTH}
      />
      <input
        className={inputClass}
        type="password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        placeholder={t('account.password')}
        autoComplete={registering ? 'new-password' : 'current-password'}
        aria-label={t('account.password')}
        maxLength={MAX_ACCOUNT_PASSWORD_LENGTH}
      />
      {registering && <p className="text-sm text-slate-500">{t('account.passwordHint')}</p>}
      <button
        type="submit"
        disabled={busy || !username.trim() || (registering ? password.length < MIN_ACCOUNT_PASSWORD_LENGTH : !password)}
        className={primaryButton}
      >
        {registering ? t('account.signUp') : t('account.logIn')}
      </button>
      {message && (
        <p role="alert" className="text-sm text-amber-500">
          {message}
        </p>
      )}
    </form>
  );
};
