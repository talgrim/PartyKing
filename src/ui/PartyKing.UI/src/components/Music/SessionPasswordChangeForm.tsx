import { FormEvent, ReactNode, useId, useState } from 'react';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { settingsButtonClass, settingsInputClass, settingsLabelClass, settingsMessageClass } from './settingsStyles';

type Props = {
  sessionId: string;
};

const MIN_PASSWORD_LENGTH = 4;
const MAX_PASSWORD_LENGTH = 100;

/** Sets a new session password. Guests have to enter it again; whoever changes it stays in. */
export const SessionPasswordChangeForm = ({ sessionId }: Props): ReactNode => {
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
      setMessage('Password changed. Guests have to enter the new one.');
    } catch (error) {
      setMessage((error as { message?: string })?.message || 'Something went wrong');
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={submit}>
      <label htmlFor={inputId} className={settingsLabelClass}>
        New password
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
          Change password
        </button>
      </div>
      <p className="mt-2 text-xs text-slate-500">
        At least {MIN_PASSWORD_LENGTH} characters. Guests have to enter the new password again.
      </p>
      {message && <p className={`mt-2 ${settingsMessageClass}`}>{message}</p>}
    </form>
  );
};
