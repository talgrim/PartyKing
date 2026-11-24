import { ReactNode, useState } from 'react';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { settingsMessageClass } from './settingsStyles';
import { useTranslation } from 'react-i18next';

type Props = {
  sessionId: string;
  sessionName: string;
  /** Called after the session was deleted; the page it was on no longer has anything to show. */
  onDeleted: () => void;
};

/** Deletes the session at once, after a confirmation. For the host and admins; the server refuses everyone else. */
export const DeleteSessionForm = ({ sessionId, sessionName, onDeleted }: Props): ReactNode => {
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const remove = async () => {
    if (!window.confirm(t('sessionDeletion.deleteConfirm', { name: sessionName }))) return;

    setBusy(true);
    setMessage(null);
    try {
      await sessionsApi.deleteSession(sessionId);
      onDeleted();
    } catch (error) {
      setMessage((error as { message?: string })?.message || t('common.somethingWentWrong'));
      setBusy(false);
    }
  };

  return (
    <div>
      <p className="mb-3 text-sm text-slate-400">{t('sessionDeletion.deleteText')}</p>
      <button
        onClick={() => void remove()}
        disabled={busy}
        className="px-3 py-2 text-red-300 border border-red-500 rounded-md cursor-pointer hover:bg-red-950 disabled:opacity-50 disabled:cursor-default"
      >
        {t('sessionDeletion.deleteButton')}
      </button>
      {message && <p className={`mt-2 ${settingsMessageClass}`}>{message}</p>}
    </div>
  );
};
