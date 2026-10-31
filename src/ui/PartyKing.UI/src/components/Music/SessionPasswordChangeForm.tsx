import { FormEvent, ReactNode, useId, useState } from 'react';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { settingsButtonClass, settingsInputClass, settingsLabelClass, settingsMessageClass } from './settingsStyles';
import { useTranslation } from 'react-i18next';

type Props = {
  sessionId: string;
};

const MIN_PASSWORD_LENGTH = 4;
const MAX_PASSWORD_LENGTH = 100;

/** Sets a new session password. Guests have to enter it again; whoever changes it stays in. */
export const SessionPasswordChangeForm = ({ sessionId }: Props): ReactNode => {
  const { t } = useTranslation();
  const inputId = useId();
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setMessage(null);
    try {
      await sessionsApi.changePassword(sessionId, password);
      setPassword('');
      setMessage(t('passwordChange.changed'));
    } catch (error) {
      setMessage((error as { message?: string })?.message || t('common.somethingWentWrong'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={submit}>
      <label htmlFor={inputId} className={settingsLabelClass}>
        {t('passwordChange.label')}
      </label>
      <div className="flex gap-2">
        <input
          id={inputId}
          className={settingsInputClass}
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          autoComplete="new-password"
          minLength={MIN_PASSWORD_LENGTH}
          maxLength={MAX_PASSWORD_LENGTH}
        />
        <button
          type="submit"
          disabled={busy || password.length < MIN_PASSWORD_LENGTH}
          className={settingsButtonClass}
        >
          {t('passwordChange.change')}
        </button>
      </div>
      <p className="mt-2 text-xs text-slate-500">
        {t('passwordChange.hint', { count: MIN_PASSWORD_LENGTH })}
      </p>
      {message && <p className={`mt-2 ${settingsMessageClass}`}>{message}</p>}
    </form>
  );
};
