import { ReactNode, useState } from 'react';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import { useIsDemo } from '@/service/endpoints/demo/useDemoConfig';
import { SessionDto } from '@/service/endpoints/sessions/types';
import { sessionsApi } from '@/service/endpoints/sessions/sessionsApi';
import { SESSION_DELETION_DELAY_DAYS } from './sessionDeletion';
import { useTranslation } from 'react-i18next';

type Props = {
  session: SessionDto;
  /** Called after the request was accepted, so the page shows the date of the deletion. */
  onRequested: () => void;
  /** Called with the reason when the request was refused or could not be made, for the page to show. */
  onFailed: (message: string) => void;
};

/**
 * Icon with which a guest asks for the session to be deleted after a delay. It asks for a confirmation first, and
 * disappears once a deletion is scheduled: the first request counts and nothing a guest does can move it. Nothing in a
 * demo, whose sessions end on their own.
 */
export const RequestSessionDeletion = ({ session, onRequested, onFailed }: Props): ReactNode => {
  const { t } = useTranslation();
  const isDemo = useIsDemo();
  const [busy, setBusy] = useState(false);

  if (isDemo || session.deletionScheduledAt) return null;

  const request = async () => {
    if (!window.confirm(t('sessionDeletion.requestConfirm', { days: SESSION_DELETION_DELAY_DAYS }))) return;

    setBusy(true);
    try {
      await sessionsApi.requestDeletion(session.id);
      onRequested();
    } catch (e) {
      onFailed((e as { message?: string })?.message || t('sessionDeletion.requestFailed'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <button
      onClick={() => void request()}
      disabled={busy}
      aria-label={t('sessionDeletion.requestTitle')}
      title={t('sessionDeletion.requestTitle')}
      className="p-1 text-slate-400 hover:text-slate-200 cursor-pointer disabled:opacity-50"
    >
      <DeleteOutlineIcon />
    </button>
  );
};
