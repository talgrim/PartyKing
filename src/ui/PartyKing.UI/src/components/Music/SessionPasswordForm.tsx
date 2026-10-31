import { FormEvent, ReactNode, useState } from 'react';
import { HTTP_STATUS_CODE } from '@/service/constants';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { useTranslation } from 'react-i18next';

type Props = {
  sessionId: string;
  onJoined: () => void;
};

/** Asks for the session password; every visitor, admins included, has to enter it before seeing the session. */
export const SessionPasswordForm = ({ sessionId, onJoined }: Props): ReactNode => {
  const { t } = useTranslation();
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
          ? t('sessionPassword.wrong')
          : status === HTTP_STATUS_CODE.TOO_MANY_REQUESTS
            ? t('sessionPassword.tooMany')
            : text || t('common.somethingWentWrong'),
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="px-2 pt-4 space-y-2" onSubmit={submit}>
      <h2 className="text-xl text-slate-200">{t('sessionPassword.title')}</h2>
      <input
        className="w-full px-2 py-2 border-1 border-gray-700 rounded-sm focus:outline-none"
        type="password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        placeholder={t('sessionPassword.label')}
        autoComplete="off"
        aria-label={t('sessionPassword.label')}
        autoFocus
      />
      <button
        type="submit"
        disabled={busy || !password}
        className="px-3 py-2 bg-amber-600 text-slate-200 rounded-md cursor-pointer disabled:opacity-50"
      >
        {t('sessionPassword.join')}
      </button>
      {message && <p className="text-sm text-amber-500">{message}</p>}
    </form>
  );
};
