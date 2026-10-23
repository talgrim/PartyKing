import { FormEvent, ReactNode, useState } from 'react';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';

type Props = {
  sessionId: string;
  onJoined: () => void;
};

/** Asks for the session password; every visitor, admins included, has to enter it before seeing the session. */
export const SessionPasswordForm = ({ sessionId, onJoined }: Props): ReactNode => {
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setMessage(null);
    try {
      await sessionsApi.join(sessionId, password);
      setPassword('');
      onJoined();
    } catch (error) {
      const { status, message: text } = (error as { status?: number; message?: string }) ?? {};
      setMessage(
        status === HTTP_STATUS_CODE.UNAUTHORIZED
          ? 'Wrong password'
          : status === HTTP_STATUS_CODE.TOO_MANY_REQUESTS
            ? 'Too many attempts, try again in a minute'
            : text || 'Something went wrong',
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="px-2 pt-4 space-y-2" onSubmit={submit}>
      <h2 className="text-xl text-slate-200">This session is password protected</h2>
      <input
        className="w-full px-2 py-2 border-1 border-gray-700 rounded-sm focus:outline-none"
        type="password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        placeholder="Session password"
        autoComplete="off"
        aria-label="Session password"
        autoFocus
      />
      <button
        type="submit"
        disabled={busy || !password}
        className="px-3 py-2 bg-amber-600 text-slate-200 rounded-md cursor-pointer disabled:opacity-50"
      >
        Join
      </button>
      {message && <p className="text-sm text-amber-500">{message}</p>}
    </form>
  );
};
